using Infrastructure;
using System.Text;
using ZR.Common;
using ZR.Common.Security;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Model.System.Enums;
using ZR.OAuth;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 绑定/解绑与内部辅助（与登录主流程拆开，单文件过长不利于阅读）
    /// </summary>
    public partial class SysThirdAccountService
    {
        /// <summary>
        /// 已登录用户绑定第三方账号。绑定成功只建立关系，不重签 token。
        /// </summary>
        public async Task Bind(ThirdBindDto dto, long userId)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Code))
            {
                throw new CustomException("缺少授权码");
            }

            var settings = RequireEnabledSettings();
            var stateCache = new OAuthStateCache(settings.StateMinutes);
            var ctx = stateCache.GetContext(dto.State);
            if (ctx == null)
            {
                throw new CustomException("授权已失效，请重新发起");
            }
            if (!string.Equals(ctx.Scene, OAuthScene.Bind, StringComparison.OrdinalIgnoreCase))
            {
                throw new CustomException("该授权不是绑定用的，请从绑定入口重新发起");
            }
            if (ctx.BindUserId != userId)
            {
                // 绑定意图绑定到发起人：否则 A 发起的授权可以被 B 拿去绑到自己账号
                throw new CustomException("授权信息与当前用户不匹配");
            }

            sysTenantService.CheckTenant(ctx.TenantId);
            using var tenantScope = TenantContext.Change(ctx.TenantId);

            var provider = OAuthProviderFactory.Create(ctx.AuthSource, ctx.TenantId, settings);
            ExternalUserInfo identity = await provider.ExchangeAsync(dto.Code, dto.State, ctx.TenantId);
            if (identity == null || string.IsNullOrWhiteSpace(identity.UnionKey))
            {
                throw new CustomException("第三方未返回可用的用户标识");
            }

            var exist = FindBind(identity.AuthSource, identity.UnionKey);
            if (exist != null)
            {
                if (exist.UserId == userId)
                {
                    return;// 幂等：已绑定同一账号，重复提交不算错误
                }
                throw new CustomException("该第三方账号已绑定其他用户，请先在原账号解绑");
            }

            var sameChannel = GetFirst(it => it.UserId == userId
                && it.AuthSource == identity.AuthSource
                && it.Status == ThirdAccountStatus.Normal);
            if (sameChannel != null)
            {
                throw new CustomException("当前账号已绑定该渠道的其他第三方账号，请先解绑");
            }

            SaveBind(userId, identity, ctx.TenantId);
            logger.Info($"第三方账号绑定成功：UserId={userId}, AuthSource={identity.AuthSource}, TenantId={ctx.TenantId}");
        }

        /// <summary>
        /// 解绑：置为已解绑而非删除，保留"谁在何时用哪个身份登录过"的审计线索
        /// </summary>
        public int Unbind(string authSource, string tenantId, long userId)
        {
            if (string.IsNullOrWhiteSpace(authSource))
            {
                throw new CustomException("缺少渠道参数");
            }

            sysTenantService.CheckTenant(tenantId);
            using var tenantScope = TenantContext.Change(tenantId);

            string source = authSource.Trim().ToUpperInvariant();
            int rows = Context.Updateable<SysThirdAccount>()
                .SetColumns(it => new SysThirdAccount
                {
                    Status = ThirdAccountStatus.Unbound,
                    Update_time = DateTime.Now
                })
                .Where(it => it.UserId == userId
                    && it.AuthSource == source
                    && it.Status == ThirdAccountStatus.Normal)
                .ExecuteCommand();

            if (rows > 0)
            {
                logger.Info($"第三方账号已解绑：UserId={userId}, AuthSource={source}, TenantId={tenantId}");
            }
            return rows;
        }

        /// <summary>
        /// 当前用户已绑定的账号（不返回 openid/unionid：前端用不到，暴露只增加风险面）
        /// </summary>
        public List<ThirdAccountDto> GetUserBinds(long userId)
        {
            return Queryable()
                .Where(it => it.UserId == userId && it.Status == ThirdAccountStatus.Normal)
                .OrderBy(it => it.Id)
                .Select(it => new ThirdAccountDto
                {
                    Id = it.Id,
                    AuthSource = it.AuthSource,
                    NickName = it.NickName,
                    Avatar = it.Avatar,
                    BindTime = it.BindTime,
                    LastLoginTime = it.LastLoginTime,
                    Status = it.Status
                })
                .ToList();
        }

        #region 内部实现

        private static OAuthSettings RequireEnabledSettings()
        {
            var settings = OAuthSettings.Current;
            if (!settings.Enabled)
            {
                throw new CustomException("第三方登录未启用");
            }
            return settings;
        }

        private SysLogininfor NewLoginInfo(ThirdLoginDto dto, OAuthStateContext ctx, ExternalUserInfo identity)
        {
            var logininfor = new SysLogininfor
            {
                LoginLocation = dto.LoginLocation,
                // 精确到渠道：审计时"用微信进"和"用钉钉进"是两件事
                AuthType = LoginAuthType.ForOAuth(identity.AuthSource),
                LoginTime = DateTime.Now,
                Ipaddr = dto.LoginIP,
                ClientId = ctx.ClientId,
                Status = "1"
            };

            var clientInfo = httpContextAccessor.HttpContext?.GetClientInfo();
            if (clientInfo != null)
            {
                logininfor.Browser = clientInfo.ToString();
                logininfor.Os = clientInfo.OS.ToString();
            }
            return logininfor;
        }

        private SysThirdAccount FindBind(string authSource, string thirdKey)
        {
            return GetFirst(it => it.AuthSource == authSource
                && it.ThirdKey == thirdKey
                && it.Status == ThirdAccountStatus.Normal);
        }

        /// <summary>
        /// 保存绑定。唯一索引是 (AuthSource, ThirdKey)，解绑后再绑定必须"复活"旧记录，
        /// 直接 INSERT 会撞唯一索引。
        /// </summary>
        private SysThirdAccount SaveBind(long userId, ExternalUserInfo identity, string tenantId)
        {
            var entity = GetFirst(it => it.AuthSource == identity.AuthSource && it.ThirdKey == identity.UnionKey);
            if (entity == null)
            {
                entity = new SysThirdAccount
                {
                    UserId = userId,
                    AuthSource = identity.AuthSource,
                    ThirdKey = identity.UnionKey,
                    Status = ThirdAccountStatus.Normal,
                    BindTime = DateTime.Now,
                    Create_by = "oauth"
                };
                FillFromIdentity(entity, identity);
                entity.Id = Insertable(entity).ExecuteReturnIdentity();
                return entity;
            }

            entity.UserId = userId;
            entity.Status = ThirdAccountStatus.Normal;
            entity.BindTime = DateTime.Now;
            FillFromIdentity(entity, identity);
            Context.Updateable(entity).UpdateColumns(it => new
            {
                it.UserId,
                it.Status,
                it.BindTime,
                it.NickName,
                it.Avatar,
                it.OpenId,
                it.UnionId,
                it.Email,
                it.LastLoginTime
            }).ExecuteCommand();
            return entity;
        }

        /// <summary>登录成功时同步第三方展示字段（昵称/头像会变，但这只是展示副本）</summary>
        private void RefreshBind(SysThirdAccount account, ExternalUserInfo identity)
        {
            FillFromIdentity(account, identity);
            Context.Updateable(account).UpdateColumns(it => new
            {
                it.NickName,
                it.Avatar,
                it.OpenId,
                it.UnionId,
                it.Email,
                it.LastLoginTime
            }).ExecuteCommand();
        }

        private static void FillFromIdentity(SysThirdAccount entity, ExternalUserInfo identity)
        {
            entity.OpenId = identity.OpenId.TruncateTrimmed(128);
            entity.UnionId = identity.UnionId.TruncateTrimmed(128);
            entity.NickName = identity.NickName.TruncateTrimmed(64);
            entity.Avatar = identity.Avatar.TruncateTrimmed(512);
            entity.Email = identity.Email.TruncateTrimmed(128);
            entity.LastLoginTime = DateTime.Now;
        }

        /// <summary>
        /// 自动建号并绑定。不调用 <c>ISysUserService.InsertUser</c>：那条路径会给新用户发
        /// "请使用初始密码登录"的消息，而第三方建号没有初始密码。
        ///
        /// 建号与绑定放在同一事务里：若只建号成功、绑定失败，会留下一个永远登不进的孤儿账号，
        /// 且下次登录会再建一个，越积越多。
        /// </summary>
        private (SysUser user, SysThirdAccount account) CreateUserAndBind(ExternalUserInfo identity, OAuthSettings settings, string tenantId)
        {
            string userName = BuildUserName(identity.AuthSource);
            var user = new SysUser
            {
                UserName = userName,
                NickName = identity.NickName.TruncateTrimmed(30) ?? userName,
                Avatar = identity.Avatar.TruncateTrimmed(200),
                Email = identity.Email.TruncateTrimmed(50),
                UserType = "00",
                Status = 0,
                DelFlag = DelFlagEnum.Normal,
                DeptId = settings.AutoCreateDeptId,
                // 密码列非空（NOT NULL），填一个没人知道的随机值：
                // 该账号只能用第三方入口登录，用这个密码永远登不进来
                Password = PasswordHasher.Hash(SecurityHelper.Md5(Guid.NewGuid().ToString())),
                Create_by = "oauth",
                Remark = $"第三方登录自动创建（{identity.AuthSource}）"
            };

            SysThirdAccount account = null;
            var result = UseTran(() =>
            {
                user.UserId = Context.Insertable(user).ExecuteReturnIdentity();

                var roleIds = settings.AutoCreateRoleIds ?? [];
                var roles = roleIds.Where(x => x > 0)
                    .Select(x => new SysUserRole { UserId = user.UserId, RoleId = x })
                    .ToList();
                if (roles.Count > 0)
                {
                    Context.Insertable(roles).ExecuteCommand();
                }

                account = SaveBind(user.UserId, identity, tenantId);
            });

            if (!result.IsSuccess)
            {
                logger.Error(result.ErrorException, $"第三方自动建号失败：AuthSource={identity.AuthSource}, TenantId={tenantId}");
                throw new CustomException("创建账号失败，请联系管理员");
            }
            return (user, account);
        }

        /// <summary>
        /// 生成初始账号名：{渠道}_{6位随机}。只有开启 OAuth:AutoCreate 才会走到这里，
        /// 默认 BindOnly 不建号（新用户由管理员建立后自助绑定），因此不追求可读性。
        /// </summary>
        private string BuildUserName(string authSource)
        {
            for (int i = 0; i < 10; i++)
            {
                string name = $"{ChannelPrefix(authSource)}_{Random.Shared.Next(100000, 999999)}";
                if (!Context.Queryable<SysUser>().Any(it => it.UserName == name))
                {
                    return name;
                }
            }
            throw new CustomException("生成账号名失败，请重试");
        }

        /// <summary>渠道前缀：只保留小写字母数字并保证以字母开头（^[a-z][a-z0-9-_]*$）</summary>
        private static string ChannelPrefix(string authSource)
        {
            string raw = (authSource ?? string.Empty).ToLowerInvariant();
            var sb = new StringBuilder(8);
            foreach (char c in raw)
            {
                if (sb.Length >= 8) break;
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
            }

            string value = sb.ToString().TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            return value.Length > 0 ? value : "oauth";
        }


        #endregion
    }
}