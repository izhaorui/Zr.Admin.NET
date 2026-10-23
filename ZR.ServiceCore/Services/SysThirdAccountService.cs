using Infrastructure;
using Infrastructure.Attribute;
using Microsoft.AspNetCore.Http;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Model.System.Enums;
using ZR.ServiceCore.OAuth;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 第三方账号绑定与登录。
    ///
    /// 关键取舍：
    /// 1) 不在这里签 token，一律回到 <see cref="ISysLoginService.SignIn"/>，
    ///    否则会绕过双因子、单会话、设备登记与登录审计。
    /// 2) 租户取自 state 载荷，不取请求参数：参数用户可改，改一下就能进别家租户。
    /// 3) 未绑定默认不建号（<see cref="ThirdAutoCreateMode.BindOnly"/>），
    ///    自动建号等于把"谁能进系统"的决定权交给第三方平台。
    /// </summary>
    [AppService(ServiceType = typeof(ISysThirdAccountService), ServiceLifetime = LifeTime.Transient)]
    public partial class SysThirdAccountService : BaseService<SysThirdAccount>, ISysThirdAccountService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly ISysLoginService sysLoginService;
        private readonly ISysUserService sysUserService;
        private readonly ISysTwoFactorService twoFactorService;
        private readonly ISysTenantService sysTenantService;
        private readonly IHttpContextAccessor httpContextAccessor;

        public SysThirdAccountService(
            ISysLoginService sysLoginService,
            ISysUserService sysUserService,
            ISysTwoFactorService twoFactorService,
            ISysTenantService sysTenantService,
            IHttpContextAccessor httpContextAccessor)
        {
            this.sysLoginService = sysLoginService;
            this.sysUserService = sysUserService;
            this.twoFactorService = twoFactorService;
            this.sysTenantService = sysTenantService;
            this.httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// 生成授权跳转地址。state 由服务端预先生成并登记上下文，
        /// 再交给上游 authorize() 拼进 URL（上游会同时写自己的 state 缓存）。
        /// </summary>
        public string BuildAuthorizeUrl(ThirdAuthorizeDto dto, long bindUserId = 0)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.AuthSource))
            {
                throw new CustomException("缺少 authSource 参数");
            }

            var settings = RequireEnabledSettings();
            sysTenantService.CheckTenant(dto.TenantId);
            using var tenantScope = TenantContext.Change(dto.TenantId);

            string source = dto.AuthSource.Trim().ToUpperInvariant();
            var provider = OAuthProviderFactory.Create(source, dto.TenantId, settings);
            if (!provider.SupportsWebAuthorize)
            {
                // 小程序这类端内渠道没有授权页，误调此接口时给出可执行的指引，而不是上游的通用报错
                throw new CustomException($"渠道 {source} 无需跳转授权页，请在端内调用登录并携带 code");
            }

            string scene = bindUserId > 0 ? OAuthScene.Bind : OAuthScene.Login;
            var stateCache = new OAuthStateCache(settings.StateMinutes);
            string state = Guid.NewGuid().ToString("N");
            stateCache.SetContext(state, new OAuthStateContext
            {
                AuthSource = source,
                TenantId = dto.TenantId,
                ClientId = dto.ClientId,
                Scene = scene,
                BindUserId = bindUserId
            });

            logger.Info($"发起第三方授权：AuthSource={source}, Scene={scene}, TenantId={dto.TenantId}");
            return provider.BuildAuthorizeUrl(state, dto.TenantId);
        }

        /// <summary>
        /// 浏览器回调要 302 到的前端页面地址，按租户解析。
        ///
        /// SaaS 下每个租户前端域名不同，而第三方只认开放平台登记的后端回调地址：
        /// 302 目标若写死成一个域名，A 租户的用户授权完会被送到 B 租户的登录页。
        /// 取值优先级：
        /// 1) OAuth:FrontendCallbackUrls:{tenantId}   —— 域名与 sys_tenant.Domain 不一致时显式指定
        /// 2) 租户绑定域名 + OAuth:FrontendCallbackPath —— 复用中间件解析租户的同一份域名数据
        /// 3) OAuth:FrontendCallbackUrl              —— 单租户/统一入口兜底
        /// 三个来源都是配置或主库数据，不接受请求参数，因此不构成开放重定向。
        /// </summary>
        public string ResolveFrontendCallbackUrl(string tenantId)
        {
            var settings = OAuthSettings.Current;

            if (!string.IsNullOrWhiteSpace(tenantId)
                && settings.FrontendCallbackUrls != null
                && settings.FrontendCallbackUrls.TryGetValue(tenantId, out string explicitUrl)
                && !string.IsNullOrWhiteSpace(explicitUrl))
            {
                return explicitUrl.Trim();
            }

            string host = GetTenantFrontendHost(tenantId);
            if (!string.IsNullOrWhiteSpace(host))
            {
                string path = settings.FrontendCallbackPath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = OAuthSettings.DefaultFrontendCallbackPath;
                }
                path = path.Trim();
                if (!path.StartsWith('/'))
                {
                    path = "/" + path;
                }
                return $"https://{host}{path}";
            }

            return settings.FrontendCallbackUrl;
        }

        /// <summary>
        /// 租户前端域名：完整域名（acme.com）直接可用，纯子域标签（acme）补配置的根域名（acme.shop.com）。
        /// 规则与 TenantResolveMiddleware 一致，否则会出现"能按域名进租户、却跳不回该域名"。
        /// </summary>
        private string GetTenantFrontendHost(string tenantId)
        {
            // 主租户（平台自用）没有 sys_tenant 记录，交给全局配置
            if (string.IsNullOrWhiteSpace(tenantId)
                || string.Equals(tenantId, App.MainDbConfigId, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var tenant = sysTenantService.GetByTenantId(tenantId);
            string value = string.IsNullOrWhiteSpace(tenant?.Domain) ? tenantId : tenant.Domain.Trim();

            // 防御管理员把协议一起填进 Domain，避免拼出 https://https://acme.com
            int schemeIndex = value.IndexOf("://", StringComparison.Ordinal);
            if (schemeIndex >= 0)
            {
                value = value.Substring(schemeIndex + 3);
            }
            value = value.TrimEnd('/').Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (value.Contains('.'))
            {
                return value;
            }

            string root = App.GetTenantRootDomains()?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim().TrimStart('.');
            return string.IsNullOrWhiteSpace(root) ? null : $"{value}.{root}";
        }

        /// <summary>
        /// App/小程序预发 state。端上拿到 code 后与 state 一并回传。
        /// 本方法未登录也可调用，Controller 侧必须限流，防止被刷缓存。
        /// </summary>
        public string IssueAppState(ThirdStateDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.AuthSource))
            {
                throw new CustomException("缺少 authSource 参数");
            }

            var settings = RequireEnabledSettings();
            sysTenantService.CheckTenant(dto.TenantId);

            string source = dto.AuthSource.Trim().ToUpperInvariant();
            // 只校验配置存在，不构造 provider：预发 state 阶段还不需要换 token 的能力
            if (settings.Resolve(source, dto.TenantId) == null)
            {
                throw new CustomException($"渠道 {source} 未配置，请联系管理员");
            }

            var stateCache = new OAuthStateCache(settings.StateMinutes);
            return stateCache.IssueWithContext(new OAuthStateContext
            {
                AuthSource = source,
                TenantId = dto.TenantId,
                ClientId = dto.ClientId,
                Scene = OAuthScene.Login
            });
        }

        /// <summary>
        /// 回调登录：state → code 换身份 → 查绑定/建号 → 双因子闸门 → 签发 token
        /// </summary>
        public async Task<LoginResultDto> LoginByCode(ThirdLoginDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Code))
            {
                throw new CustomException("缺少授权码，请重新发起登录");
            }

            var settings = RequireEnabledSettings();
            var stateCache = new OAuthStateCache(settings.StateMinutes);
            var ctx = stateCache.GetContext(dto.State);
            if (ctx == null)
            {
                throw new CustomException("授权已失效，请重新发起登录");
            }
            if (!string.Equals(ctx.Scene, OAuthScene.Login, StringComparison.OrdinalIgnoreCase))
            {
                // 绑定场景的 state 不能拿来登录，否则等于用"绑定授权"签发登录态
                throw new CustomException("该授权仅用于绑定，不能用于登录");
            }

            sysTenantService.CheckTenant(ctx.TenantId);

            // 本服务的 Context 在构造时按请求头/域名固定，之后的 TenantContext.Change 不会换库。
            // 请求租户与 state 租户不一致时，查绑定与自动建号都会落到对方租户库
            // （等于用自己的一次授权在别人租户里建号），必须直接拒绝而不是静默查错库。
            string requestTenantId = App.GetCurrentTenantId();
            if (!string.Equals(requestTenantId, ctx.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                logger.Warn($"第三方登录租户不一致已拒绝：StateTenant={ctx.TenantId}, RequestTenant={requestTenantId}, AuthSource={ctx.AuthSource}");
                throw new CustomException("登录租户与授权信息不匹配，请返回登录页重新发起");
            }

            using var tenantScope = TenantContext.Change(ctx.TenantId);

            var provider = OAuthProviderFactory.Create(ctx.AuthSource, ctx.TenantId, settings);
            // state 在 ExchangeAsync 内一次性消费；后续失败也不归还——授权码已被使用，重放无意义
            ExternalUserInfo identity = await provider.ExchangeAsync(dto.Code, dto.State, ctx.TenantId);
            if (identity == null || string.IsNullOrWhiteSpace(identity.UnionKey))
            {
                throw new CustomException("第三方未返回可用的用户标识");
            }

            var logininfor = NewLoginInfo(dto, ctx, identity);
            var account = FindBind(identity.AuthSource, identity.UnionKey);

            SysUser user;
            if (account == null)
            {
                // 自动建号需要"开关 + 至少一个角色"同时成立。只开 AutoCreate 而不配角色，
                // 建出来的账号没有任何菜单，登录进去就是空白后台，还会在库里留下一个
                // 没人知道密码的用户名。这种配置错误按"未绑定"处理，让新用户回到管理员建号的流程。
                bool autoCreateEnabled = settings.AutoCreate == ThirdAutoCreateMode.AutoCreate
                    && settings.AutoCreateRoleIds?.Any(x => x > 0) == true;
                if (!autoCreateEnabled)
                {
                    logger.Info($"第三方登录未绑定，需先绑定：AuthSource={identity.AuthSource}, TenantId={ctx.TenantId}");
                    logininfor.UserName = identity.NickName ?? string.Empty;
                    logininfor.Msg = "第三方账号未绑定";
                    sysLoginService.AddLoginInfo(logininfor, ctx.TenantId);
                    return new LoginResultDto { Status = LoginResultStatus.BindRequired };
                }

                // 建号与绑定必须同一事务：只建号不绑定会留下一个永远登不进的孤儿账号
                (user, account) = CreateUserAndBind(identity, settings, ctx.TenantId);
                logger.Info($"第三方登录自动建号：UserId={user.UserId}, AuthSource={identity.AuthSource}, TenantId={ctx.TenantId}");
            }
            else
            {
                // 绕过数据权限按主键回查：登录态尚未建立，任何数据范围过滤都会误判"用户不存在"
                user = twoFactorService.GetUserForAuth(account.UserId);
                if (user == null || user.DelFlag != DelFlagEnum.Normal)
                {
                    logger.Warn($"第三方绑定指向的用户已失效：UserId={account.UserId}, AuthSource={identity.AuthSource}");
                    logininfor.UserName = account.NickName ?? string.Empty;
                    logininfor.Msg = "绑定用户不存在或已删除";
                    sysLoginService.AddLoginInfo(logininfor, ctx.TenantId);
                    return new LoginResultDto { Status = LoginResultStatus.AccountUnavailable };
                }

                // 第三方昵称/头像会变，登录时同步展示字段；不覆盖本系统昵称
                RefreshBind(account, identity);
            }

            logininfor.UserName = user.UserName;
            logininfor.UserId = user.UserId;

            if (user.Status == 1)
            {
                logininfor.Msg = "账号已停用";
                sysLoginService.AddLoginInfo(logininfor, ctx.TenantId);
                return new LoginResultDto { Status = LoginResultStatus.AccountUnavailable };
            }

            // 设备凭据只以哈希进入票据/登记链路，口径与账号密码登录一致
            var deviceTokenHash = twoFactorService.HashDeviceToken(dto.DeviceToken);
            if (twoFactorService.RequiresTwoFactor(user, dto.DeviceToken))
            {
                logger.Warn($"第三方登录校验通过、等待双因子验证：UserId={user.UserId}, AuthSource={identity.AuthSource}");
                return sysLoginService.BuildTwoFactorResult(user, ctx.TenantId, ctx.ClientId, deviceTokenHash,
                    dto.LoginIP, dto.LoginLocation, TwoFactorLoginSource.ThirdAccount, logininfor.AuthType);
            }

            logininfor.TwoFactorType = twoFactorService.IsEnabled(user)
                ? LoginTwoFactorType.TrustedDevice
                : LoginTwoFactorType.None;
            logininfor.Status = "0";
            logininfor.Msg = "登录成功";
            sysLoginService.AddLoginInfo(logininfor, ctx.TenantId);
            sysUserService.UpdateLoginInfo(dto.LoginIP, user.UserId);

            string abnormalNotice = sysLoginService.GetAbnormalLoginNotice(user, dto.LoginIP);
            return sysLoginService.SignIn(user, ctx.TenantId, ctx.ClientId, deviceTokenHash, dto.LoginIP,
                abnormalNotice: abnormalNotice);
        }
    }
}