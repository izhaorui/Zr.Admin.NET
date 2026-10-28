using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Model;
using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using UAParser;
using ZR.Common;
using ZR.Common.Security;
using ZR.Infrastructure.Constant;
using ZR.Infrastructure.Helper;
using ZR.Infrastructure.IPTools;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Repository;
using ZR.ServiceCore.Model.Dto;
using ZR.ServiceCore.Resources;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 登录
    /// </summary>
    [AppService(ServiceType = typeof(ISysLoginService), ServiceLifetime = LifeTime.Transient)]
    public class SysLoginService : BaseService<SysLogininfor>, ISysLoginService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly ISysUserService SysUserService;
        private readonly ISysUserMsgService sysUserMsgService;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ISysTwoFactorService TwoFactorService;
        private readonly ISysWebAuthnService WebAuthnService;
        private readonly ISysRoleService roleService;
        private readonly ISysPermissionService permissionService;
        // 登录失败锁定策略（sys.account.*）在主库 sys_config，必须经 ISysConfigService 读取
        private readonly ISysConfigService sysConfigService;

        public SysLoginService(
            ISysUserService sysUserService, 
            ISysUserMsgService sysUserMsgService,
            IHttpContextAccessor httpContextAccessor,
            IStringLocalizer<SharedResource> localizer,
            ISysTwoFactorService twoFactorService,
            ISysWebAuthnService webAuthnService,
            ISysRoleService roleService,
            ISysPermissionService permissionService,
            ISysConfigService sysConfigService)
        {
            SysUserService = sysUserService;
            this.sysUserMsgService = sysUserMsgService;
            this.httpContextAccessor = httpContextAccessor;
            _localizer = localizer;
            TwoFactorService = twoFactorService;
            WebAuthnService = webAuthnService;
            this.roleService = roleService;
            this.permissionService = permissionService;
            this.sysConfigService = sysConfigService;
        }

        /// <summary>双因子配置（与 <see cref="SysTwoFactorService"/> 同一节点，避免各处重复 Bind）</summary>
        private static TwoFactorSettings TwoFactorOptions => AppSettings.Bind("TwoFactor", new TwoFactorSettings());

        /// <summary>
        /// 账号密码登录的完整流程：校验账号密码 → 双因子闸门 → 签发 token。
        /// </summary>
        /// <param name="loginBody">登录请求</param>
        /// <param name="location">登录地点（调用方按 IP 解析后传入）</param>
        public LoginResultDto Login(LoginBodyDto loginBody, string location)
        {
            // 审计字段（地点/第一步凭据）在服务内固定，调用方不必再"记得"赋值
            var logininfor = new SysLogininfor
            {
                LoginLocation = location,
                AuthType = LoginAuthType.Password
            };

            if (loginBody.Password.Length != 32)
            {
                loginBody.Password = SecurityHelper.Md5(loginBody.Password);
            }
            SysUser user = SysUserService.Login(loginBody);
            logininfor.UserName = loginBody.Username;
            logininfor.Status = "1";
            logininfor.LoginTime = DateTime.Now;
            logininfor.Ipaddr = loginBody.LoginIP;
            logininfor.ClientId = loginBody.ClientId;

            ClientInfo clientInfo = httpContextAccessor.HttpContext.GetClientInfo();
            logininfor.Browser = clientInfo.ToString();
            logininfor.Os = clientInfo.OS.ToString();

            if (user == null || user.UserId <= 0)
            {
                logininfor.Msg = _localizer["login_pwd_error"].Value;
                // 凭据错误计入失败次数，达到阈值即锁定（此前只读不写，锁定永远不会生效）。
                // 放在写日志之前：日志落库异常不应导致锁定失效。
                RecordLoginFailure(loginBody.Username);
                AddLoginInfo(logininfor, loginBody.TenantId);
                throw new CustomException(ResultCode.LOGIN_ERROR, logininfor.Msg, false);
            }
            // 密码已校验通过：清空历史失败次数，避免"错几次后成功登录"仍被后续误锁
            ClearLoginFailure(user.UserName);
            if (PasswordHasher.NeedsUpgrade(user.Password))
            {
                SysUserService.UpgradePasswordHash(user.UserId, loginBody.Password);
            }
            logininfor.UserId = user.UserId;
            if (user.Status == 1)
            {
                logininfor.Msg = _localizer["login_user_disabled"].Value;//該用戶已禁用
                AddLoginInfo(logininfor, loginBody.TenantId);
                throw new CustomException(ResultCode.LOGIN_ERROR, logininfor.Msg, false);
            }

            // 设备凭据只以哈希形式进入票据/登记链路，全程不落明文
            var deviceTokenHash = TwoFactorService.HashDeviceToken(loginBody.DeviceToken);

            // 双因子待验证：登录尚未完成，此处不写登录日志也不更新最后登录信息，
            // 由第二步（/login/2fa/verify）按最终结果落一条记录，避免半程登录被统计成成功。
            // 判定依据是服务端签发的设备凭据（DeviceToken），不是客户端指纹
            if (TwoFactorService.RequiresTwoFactor(user, loginBody.DeviceToken))
            {
                logger.Warn($"账号密码校验通过、等待双因子验证：UserName={loginBody.Username}, UserId={user.UserId}");
                return BuildTwoFactorResult(user, loginBody.TenantId, loginBody.ClientId, deviceTokenHash,
                    loginBody.LoginIP, location, TwoFactorLoginSource.Account);
            }

            // 能走到签发说明本次不需要第二因子：要么未启用 2FA，要么命中可信设备。
            // 后者意味着"本次并未校验动态码"，审计时必须能看出来，故单独标记。
            logininfor.TwoFactorType = TwoFactorService.IsEnabled(user)
                ? LoginTwoFactorType.TrustedDevice
                : LoginTwoFactorType.None;

            logininfor.Status = "0";
            logininfor.Msg = "登录成功";
            AddLoginInfo(logininfor, loginBody.TenantId);
            SysUserService.UpdateLoginInfo(loginBody.LoginIP, user.UserId);

            // 与既有口径一致：异地提醒用本次登录**之前**的 IP 判断（取的是上面这份内存实体）
            string abnormalNotice = GetAbnormalLoginNotice(user, loginBody.LoginIP);
            return SignIn(user, loginBody.TenantId, loginBody.ClientId, deviceTokenHash, loginBody.LoginIP,
                abnormalNotice: abnormalNotice);
        }

        /// <summary>
        /// 手机号登录的完整流程：校验账号状态 → 双因子闸门 → 签发 token（语义同 <see cref="Login"/>）
        /// </summary>
        public LoginResultDto PhoneLogin(PhoneLoginDto loginBody, SysUser user, string location)
        {
            var logininfor = new SysLogininfor
            {
                LoginLocation = location,
                AuthType = LoginAuthType.SmsCode
            };

            logininfor.UserName = user.UserName;
            logininfor.UserId = user.UserId;
            logininfor.Status = "1";
            logininfor.LoginTime = DateTime.Now;
            logininfor.Ipaddr = loginBody.LoginIP;
            logininfor.ClientId = loginBody.ClientId;

            ClientInfo clientInfo = httpContextAccessor.HttpContext.GetClientInfo();
            logininfor.Browser = clientInfo.ToString();
            logininfor.Os = clientInfo.OS.ToString();

            if (user.Status == 1)
            {
                logininfor.Msg = _localizer["login_user_disabled"].Value;
                AddLoginInfo(logininfor, loginBody.TenantId);
                throw new CustomException(ResultCode.LOGIN_ERROR, logininfor.Msg, false);
            }

            // 短信验证码已校验通过，清空该账号的历史失败次数
            ClearLoginFailure(user.UserName);

            // 回查实体：短信码校验与用户查询之间可能被改过状态，按库里最新值判定
            var entity = Context.Queryable<SysUser>().Where(it => it.UserId == user.UserId).First();
            var deviceTokenHash = TwoFactorService.HashDeviceToken(loginBody.DeviceToken);

            if (TwoFactorService.RequiresTwoFactor(entity, loginBody.DeviceToken))
            {
                logger.Warn($"手机号登录校验通过、等待双因子验证：UserName={user.UserName}, UserId={user.UserId}");
                return BuildTwoFactorResult(user, loginBody.TenantId, loginBody.ClientId, deviceTokenHash,
                    loginBody.LoginIP, location, TwoFactorLoginSource.Phone);
            }

            // 语义同 Login：手机号登录同样可能是"命中可信设备"而免掉动态码
            logininfor.TwoFactorType = TwoFactorService.IsEnabled(entity)
                ? LoginTwoFactorType.TrustedDevice
                : LoginTwoFactorType.None;

            logininfor.Status = "0";
            logininfor.Msg = "登录成功";
            AddLoginInfo(logininfor, loginBody.TenantId);
            SysUserService.UpdateLoginInfo(loginBody.LoginIP, user.UserId);

            return SignIn(user, loginBody.TenantId, loginBody.ClientId, deviceTokenHash, loginBody.LoginIP);
        }

        /// <summary>
        /// 双因子登录第二步：校验一次性票据 + 验证码（或一次性恢复码）后签发 token。
        ///
        /// 要点：
        /// 1) 票据先**一次性消费**，杜绝并发重放；
        /// 2) 验证失败时**归还票据**（未锁定才归还），让用户能在剩余有效期内重试输错的码；
        /// 3) 已锁定即达到失败上限，不再归还，否则持续试探可把票据续命到锁定解除；
        /// 4) 租户来自票据载荷 —— 第二步是独立请求，此时尚不知道该读哪个库。
        /// </summary>
        public LoginResultDto VerifyTwoFactor(TwoFactorVerifyDto dto)
        {
            var settings = TwoFactorOptions;
            var ticketMinutes = settings.TicketMinutes > 0 ? settings.TicketMinutes : 5;

            var payload = CacheService.ConsumeTwoFactorTicket<TwoFactorTicketPayload>(dto.Ticket, ticketMinutes);
            if (payload == null)
            {
                return new LoginResultDto { Status = LoginResultStatus.TicketExpired };
            }

            using var tenantScope = TenantContext.Change(payload.TenantId);

            var user = TwoFactorService.GetUserForAuth(payload.UserId);
            if (user == null || user.Status == 1)
            {
                return new LoginResultDto { Status = LoginResultStatus.AccountUnavailable };
            }

            var logininfor = BuildTwoFactorLogininfor(user, payload);

            TwoFactorCredential credential;
            try
            {
                credential = TwoFactorService.VerifyCodeOrThrow(user, dto.Code);
            }
            catch (Exception)
            {
                // 2FA 环节的失败是重要安全信号，必须与普通登录失败一样落库留痕后再抛出
                AddLoginInfo(logininfor, payload.TenantId);
                // 票据生命周期："验证成功"或"达到失败上限"后失效，其余失败归还、可在剩余 TTL 内重试。
                // 归还按绝对过期时间计剩余，重试不刷新生命周期。
                if (!CacheService.IsTwoFactorLocked(payload.UserId))
                {
                    CacheService.RestoreTwoFactorTicket(dto.Ticket, payload, payload.ExpiresAt);
                }
                throw;
            }

            // 记下本次到底是用动态码还是恢复码通过的：恢复码登录是审计上最需要一眼看出来的事件
            var twoFactorType = credential == TwoFactorCredential.RecoveryCode
                ? LoginTwoFactorType.RecoveryCode
                : LoginTwoFactorType.Totp;

            return CompleteTwoFactorSignIn(user, payload, logininfor, twoFactorType, dto.RememberDevice);
        }

        /// <summary>
        /// WebAuthn 登录第二步（第一段）：按票据下发认证 options。
        ///
        /// 票据在这条路径上**只读不消费**：options 与断言是两个独立请求，
        /// 若在这里消费，用户取消弹窗或指纹识别失败后票据就废了，被迫重新输密码 ——
        /// 与 TOTP"输错可重试"的现有体验不一致。
        /// </summary>
        public TwoFactorWebAuthnOptionsDto MakeWebAuthnOptions(TwoFactorWebAuthnOptionsRequestDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Ticket))
            {
                throw new CustomException("请求参数错误");
            }

            var payload = CacheService.PeekTwoFactorTicket<TwoFactorTicketPayload>(dto.Ticket);
            if (payload == null)
            {
                throw new CustomException(ResultCode.LOGIN_ERROR, "双因子验证已超时，请重新登录", false);
            }

            using var tenantScope = TenantContext.Change(payload.TenantId);

            var user = TwoFactorService.GetUserForAuth(payload.UserId);
            if (user == null || user.Status == 1)
            {
                throw new CustomException(ResultCode.LOGIN_ERROR, "账号不可用", false);
            }

            // 只给确实有可用凭据的账号下发 options：否则 AllowedCredentials 为空，
            // 浏览器会弹一个没有任何可用凭据的窗口，用户完全不知道发生了什么
            if (!TwoFactorService.GetAvailableMethods(user).Contains(TwoFactorMethod.WebAuthn))
            {
                throw new CustomException(ResultCode.LOGIN_ERROR, "当前账号未绑定 Passkey", false);
            }

            return WebAuthnService.MakeAssertionOptions(user.UserId, dto.Ticket);
        }

        /// <summary>
        /// WebAuthn 登录第二步（第二段）：校验断言并签发 token。
        ///
        /// 三步顺序不能换：
        /// 1) 先消费 challenge —— 它是本次断言的一次性凭据，校验失败也必须作废，否则可被反复重放；
        /// 2) 再校验断言（同时校验 challenge 归属：用户、用途、票据三者必须一致）；
        /// 3) 最后消费票据 —— 只有确认成功才消耗用户的登录机会。
        /// </summary>
        public async Task<LoginResultDto> VerifyTwoFactorWebAuthn(TwoFactorWebAuthnVerifyDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Ticket) || string.IsNullOrWhiteSpace(dto.Challenge))
            {
                throw new CustomException("请求参数错误");
            }

            var settings = TwoFactorOptions;
            var challengeMinutes = settings.WebAuthnChallengeMinutes > 0 ? settings.WebAuthnChallengeMinutes : 5;

            var challengePayload = CacheService.ConsumeWebAuthnChallenge<WebAuthnChallengePayload>(dto.Challenge, challengeMinutes);
            if (challengePayload == null)
            {
                throw new CustomException(ResultCode.LOGIN_ERROR, "验证已超时，请重新验证", false);
            }

            var payload = CacheService.PeekTwoFactorTicket<TwoFactorTicketPayload>(dto.Ticket);
            if (payload == null)
            {
                throw new CustomException(ResultCode.LOGIN_ERROR, "双因子验证已超时，请重新登录", false);
            }

            // 三者必须同时匹配：用途防跨流程（注册的 challenge 拿来登录）、
            // 票据防跨会话、用户防跨账号
            if (challengePayload.Type != WebAuthnChallengeType.Authenticate
                || challengePayload.Ticket != dto.Ticket
                || challengePayload.UserId != payload.UserId)
            {
                logger.Warn($"WebAuthn challenge 归属校验失败：票据={dto.Ticket}, 挑战用户={challengePayload.UserId}, 票据用户={payload.UserId}");
                throw new CustomException(ResultCode.LOGIN_ERROR, "验证信息不匹配，请重新登录", false);
            }

            using var tenantScope = TenantContext.Change(payload.TenantId);

            var user = TwoFactorService.GetUserForAuth(payload.UserId);
            if (user == null || user.Status == 1)
            {
                return new LoginResultDto { Status = LoginResultStatus.AccountUnavailable };
            }

            var logininfor = BuildTwoFactorLogininfor(user, payload);

            try
            {
                await WebAuthnService.VerifyAssertionAsync(challengePayload, dto.Assertion);
            }
            catch (Exception)
            {
                // 与 TOTP 同口径：2FA 环节失败是重要安全信号，必须落库留痕后再抛出
                AddLoginInfo(logininfor, payload.TenantId);
                throw;
            }

            // 断言已通过，此时才消费票据。并发下若另一请求先消费成功，本次不再签发，防止一票两用
            var ticketMinutes = settings.TicketMinutes > 0 ? settings.TicketMinutes : 5;
            if (CacheService.ConsumeTwoFactorTicket<TwoFactorTicketPayload>(dto.Ticket, ticketMinutes) == null)
            {
                logger.Warn($"WebAuthn 票据已被并发消费，拒绝重复签发：UserId={payload.UserId}");
                throw new CustomException(ResultCode.LOGIN_ERROR, "双因子验证已提交，请重新登录", false);
            }

            return CompleteTwoFactorSignIn(user, payload, logininfor, LoginTwoFactorType.WebAuthn, dto.RememberDevice);
        }

        /// <summary>
        /// 构造第二步登录日志。字段全部来自票据 —— 第二步是独立请求，
        /// 无法再问"用户刚才是怎么登录的"（三方登录尤其需要精确到渠道）。
        /// </summary>
        private SysLogininfor BuildTwoFactorLogininfor(SysUser user, TwoFactorTicketPayload payload)
        {
            var clientInfo = httpContextAccessor.HttpContext.GetClientInfo();
            return new SysLogininfor
            {
                UserName = user.UserName,
                UserId = user.UserId,
                AuthType = payload.AuthType ?? DefaultAuthTypeOf((TwoFactorLoginSource)payload.LoginSource),
                Ipaddr = payload.LoginIP,
                ClientId = payload.ClientId,
                LoginTime = DateTime.Now,
                LoginLocation = payload.LoginLocation,
                Browser = clientInfo.ToString(),
                Os = clientInfo.OS.ToString(),
                Status = "1",
                Msg = "双因子验证失败"
            };
        }

        /// <summary>
        /// 第二因子通过后的统一收尾：写成功日志、刷新最后登录信息、签发 token。
        /// TOTP 与 WebAuthn 共用，避免新增路径漏掉设备登记或异地提醒（历史上漏过设备登记）。
        /// </summary>
        private LoginResultDto CompleteTwoFactorSignIn(SysUser user, TwoFactorTicketPayload payload,
            SysLogininfor logininfor, string twoFactorType, bool rememberDevice)
        {
            logininfor.TwoFactorType = twoFactorType;

            // 与普通登录同一口径：先取异地提醒（用的是本次之前的登录 IP），再更新最后登录信息
            string abnormalNotice = GetAbnormalLoginNotice(user, payload.LoginIP);

            logininfor.Status = "0";
            logininfor.Msg = "登录成功";
            AddLoginInfo(logininfor, payload.TenantId);
            SysUserService.UpdateLoginInfo(payload.LoginIP, user.UserId);

            // 本次登记哪台设备由第一步票据绑定的凭据哈希决定，第二步不允许替换设备：
            // 否则拿到票据的攻击者可以把"记住此设备"写到任意设备标识上。
            return SignIn(user, payload.TenantId, payload.ClientId, payload.DeviceTokenHash, payload.LoginIP,
                rememberDevice, abnormalNotice);
        }

        /// <summary>
        /// 签发 token 的统一出口。
        ///
        /// 权限装配、单会话、"本会话已通过双因子"标记、登录设备登记四件事集中在这里，
        /// 三条登录路径共用，避免某个入口漏掉其中一步（历史上漏过设备登记）。
        /// </summary>
        public LoginResultDto SignIn(SysUser user, string tenantId, string clientId, string deviceTokenHash,
            string loginIp, bool trustDevice = false, string? abnormalNotice = null)
        {
            List<SysRole> roles = roleService.SelectUserRoleListByUserId(user.UserId);
            //权限集合 eg *:*:*,system:user:list（按套餐菜单过滤）
            List<string> permissions = permissionService.GetMenuPermission(new SysUserDto() { UserId = user.UserId });

            LoginUser loginUser = new(user.Adapt<LoginUser>(), roles.Adapt<List<Roles>>())
            {
                TenantId = tenantId,
                Permissions = permissions,
                ScopeType = ComputeScopeType(roles),
                SessionId = Guid.NewGuid().ToString("N"),// 单设备登录：每次登录生成新会话ID并缓存，旧会话将被中间件判定失效
                FirstLoginTime = DateTime.UtcNow// 绝对有效期起点：续期只改 ExpireTime，不改这个值
            };

            // 设备登记：登记失败不影响登录 —— 它不是安全闸门，只承担审计与提醒职责。
            string deviceToken = null;
            try
            {
                deviceToken = TwoFactorService.RecordLoginDevice(user.UserId, deviceTokenHash, clientId,
                    httpContextAccessor.HttpContext.GetClientInfo().ToString(), loginIp, trustDevice)?.DeviceToken;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"登录设备登记失败：UserId={user.UserId}");
            }

            CacheService.SetUserSession(loginUser.UserId, loginUser.SessionId);
            // 能走到签发说明身份校验已完整（未启用 2FA，或刚通过 2FA / 命中可信设备）。
            // 标记该会话供扫码登录判定"移动端当前会话确实过了 2FA"。
            CacheService.SetTwoFactorSessionOk(loginUser.UserId, loginUser.SessionId);

            var token = JwtUtil.GenerateJwtToken(JwtUtil.AddClaims(loginUser));
            return new LoginResultDto
            {
                Status = LoginResultStatus.Success,
                Token = token,
                DeviceToken = deviceToken,
                Notice = abnormalNotice
            };
        }

        /// <summary>
        /// 组装"需要双因子验证"的结果：写入一次性票据，返回票据与可用方法列表（**不含 token**）。
        /// 票据里带租户与本次设备凭据哈希：第二步据此查用户、登记设备，且只能用票据绑定的那台设备。
        /// </summary>
        public LoginResultDto BuildTwoFactorResult(SysUser user, string tenantId, string clientId, string deviceTokenHash,
            string loginIp, string location, TwoFactorLoginSource source, string authType = null)
        {
            var settings = TwoFactorOptions;
            var minutes = settings.TicketMinutes > 0 ? settings.TicketMinutes : 5;
            var ticket = Guid.NewGuid().ToString("N");

            CacheService.SetTwoFactorTicket(ticket, new TwoFactorTicketPayload
            {
                UserId = user.UserId,
                UserName = user.UserName,
                NickName = user.NickName,
                TenantId = tenantId,
                ClientId = clientId,
                DeviceTokenHash = deviceTokenHash,
                LoginIP = loginIp,
                LoginLocation = location,
                LoginSource = (int)source,
                // 凭据类型随票据走：第二步是独立请求，无法再问"用户刚才是怎么登录的"
                AuthType = authType ?? DefaultAuthTypeOf(source),
                ExpiresAt = DateTime.Now.AddMinutes(minutes)
            }, minutes);

            return new LoginResultDto
            {
                Status = LoginResultStatus.TwoFactorRequired,
                Ticket = ticket,
                ExpireMinutes = minutes,
                // 方法列表由服务端算、前端只按列表渲染：WebAuthn 落地时这里加一项即可
                Methods = TwoFactorService.GetAvailableMethods(user),
                PreferredMethod = TwoFactorService.GetPreferredMethod(user)
            };
        }

        /// <summary>
        /// 按第一步来源推导凭据类型（调用方未显式传入时的回退）。
        /// 第三方登录通常显式传入带渠道的码，这里只兜底到粗粒度的 oauth。
        /// </summary>
        private static string DefaultAuthTypeOf(TwoFactorLoginSource source)
        {
            return source switch
            {
                TwoFactorLoginSource.Phone => LoginAuthType.SmsCode,
                TwoFactorLoginSource.ThirdAccount => LoginAuthType.OAuth,
                _ => LoginAuthType.Password
            };
        }

        /// <summary>
        /// 查询登录日志
        /// </summary>
        /// <param name="logininfoDto"></param>
        /// <returns></returns>
        public PagedInfo<SysLogininfor> GetLoginLog(SysLogininfoQueryDto logininfoDto)
        {
            var db = ResolveTenantDb();
            var exp = Expressionable.Create<SysLogininfor>();

            exp.AndIF(logininfoDto.BeginTime == null, it => it.LoginTime >= DateTime.Now.ToShortDateString().ParseToDateTime());
            exp.AndIF(logininfoDto.BeginTime != null, it => it.LoginTime >= logininfoDto.BeginTime && it.LoginTime <= logininfoDto.EndTime);
            exp.AndIF(logininfoDto.UserId != null, it => it.UserId == logininfoDto.UserId);
            exp.AndIF(logininfoDto.Status.IfNotEmpty(), f => f.Status == logininfoDto.Status);
            exp.AndIF(logininfoDto.Ipaddr.IfNotEmpty(), f => f.Ipaddr == logininfoDto.Ipaddr);
            exp.AndIF(logininfoDto.UserName.IfNotEmpty(), f => f.UserName.Contains(logininfoDto.UserName));
            // 第三方登录落库为 "oauth:渠道"，前端筛选项只传大类 oauth，故走前缀匹配；
            // 其余凭据是固定码，仍按等值查
            exp.AndIF(logininfoDto.AuthType == LoginAuthType.OAuth, f => f.AuthType.StartsWith(LoginAuthType.OAuth));
            exp.AndIF(logininfoDto.AuthType.IfNotEmpty() && logininfoDto.AuthType != LoginAuthType.OAuth,
                f => f.AuthType == logininfoDto.AuthType);
            exp.AndIF(logininfoDto.TwoFactorType.IfNotEmpty(), f => f.TwoFactorType == logininfoDto.TwoFactorType);

            var query = db.Queryable<SysLogininfor>().ApplyScope().Where(exp.ToExpression())
                .OrderBy(it => it.InfoId, OrderByType.Desc);

            var list = query.ToPage(logininfoDto);
            list.Result.MaskField(
                HttpContextExtension.HasSensitivePerm(App.HttpContext, SensitivePerms.ViewRealIP),
                it => it.Ipaddr, (it, v) => it.Ipaddr = v, MaskUtil.MaskIp);
            // 设备指纹是可复用的设备标识，日志输出一律脱敏：登录日志对管理员/导出渠道可见，
            // 明文外泄等于把"已登记设备标识"拱手送人
            list.Result.MaskField(false, it => it.ClientId, (it, v) => it.ClientId = v, MaskUtil.MaskDeviceId);
            return list;
        }

        /// <summary>
        /// 记录登录日志
        /// </summary>
        /// <param name="sysLogininfor"></param>
        /// <param name="tenantId">租户ID，多租户模式下写入对应租户库</param>
        /// <returns></returns>
        public void AddLoginInfo(SysLogininfor sysLogininfor, string tenantId = null)
        {
            var db = ResolveTenantDb(tenantId);
            db.Insertable(sysLogininfor).ExecuteCommand();
        }

        /// <summary>
        /// 清空登录日志。
        /// <para>
        /// 清空前先按日期备份为 <c>sys_logininfor_yyyyMMdd</c>，与操作日志 <c>CleanOperLog</c> 同一口径。
        /// 等保 2.0 安全审计要求"审计记录应受到保护、定期备份，避免受到未预期的删除、修改或覆盖"，
        /// 直接 TRUNCATE 会让日志永久消失，测评时属于不符合项。
        /// </para>
        /// </summary>
        public void TruncateLogininfo()
        {
            var newTableName = $"sys_logininfor_{DateTime.Now:yyyyMMdd}";
            if (Queryable().Any() && !Context.DbMaintenance.IsAnyTable(newTableName))
            {
                Context.DbMaintenance.BackupTable("sys_logininfor", newTableName);
            }

            Truncate();
        }

        /// <summary>
        /// 删除登录日志
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public int DeleteLogininforByIds(long[] ids)
        {
            return Delete(ids);
        }

        public void CheckLockUser(string userName)
        {
            var key = NormalizeLoginKey(userName);
            if (key.Length == 0) return;

            long lockTimeStamp;
            try
            {
                lockTimeStamp = CacheService.GetLockUser(key);
            }
            catch (Exception ex)
            {
                // 锁定是辅助安全措施，缓存不可用时不能反过来阻断全部登录
                logger.Warn(ex, $"读取账号锁定状态失败，本次跳过锁定校验：Key={key}");
                return;
            }

            if (lockTimeStamp <= 0) return;

            var lockTime = DateTimeHelper.ToLocalTimeDateBySeconds(lockTimeStamp);
            var ts = lockTime - DateTime.Now;
            if (ts.TotalSeconds <= 0) return;

            var minutes = (int)Math.Round(ts.TotalMinutes, 0);
            // 锁定提醒限频：同一账号 60 分钟内只发一次，否则攻击者持续尝试会把系统消息刷爆
            try
            {
                if (minutes > 0 && CacheService.TryMarkLoginLockAlert(key, Math.Max(minutes, 60)))
                {
                    NotifyUserLocked(key, minutes);
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"账号锁定提醒发送失败：Key={key}");
            }
            throw new CustomException(ResultCode.LOGIN_ERROR, $"你的账号已被锁,剩余{minutes}分钟");
        }

        /// <summary>
        /// 记录一次登录失败：失败次数达到阈值即锁定账号。
        ///
        /// 只由"凭据错误"触发（密码错、短信验证码错）；已禁用、租户停用等业务拒绝不计入，
        /// 避免正常被禁用的账号反复累加锁定。按传入标识计数，用户不存在时同样计数 ——
        /// 既不泄露账号是否存在，也不会因枚举不存在的用户名而锁住真实用户。
        /// </summary>
        public void RecordLoginFailure(string key)
        {
            key = NormalizeLoginKey(key);
            if (key.Length == 0) return;

            try
            {
                var (maxRetryCount, lockMinutes) = GetLoginLockOptions();
                // 未配置或配置为 0 表示不启用锁定
                if (maxRetryCount <= 0 || lockMinutes <= 0) return;

                // 计数窗口不小于 30 分钟：太短会让攻击者"每次隔一会儿试一下"永远凑不满，
                // 太长则正常用户偶发输错也会被累计到锁定。
                var window = Math.Max(lockMinutes, 30);
                var failCount = CacheService.IncreaseLoginFail(key, window);
                if (failCount < maxRetryCount) return;

                // 已在锁定期内不再写锁定，避免攻击者持续尝试把解锁时间无限推后
                if (CacheService.GetLockUser(key) > 0) return;

                var lockUntil = DateTimeOffset.Now.ToUnixTimeSeconds() + lockMinutes * 60L;
                CacheService.SetLockUser(key, lockUntil, lockMinutes);
                CacheService.RemoveLoginFail(key);
                logger.Warn($"登录失败次数达到阈值，账号已锁定：Key={key}, 失败次数={failCount}, 锁定时长={lockMinutes}分钟");

                if (CacheService.TryMarkLoginLockAlert(key, Math.Max(lockMinutes, 60)))
                {
                    NotifyUserLocked(key, lockMinutes);
                }
            }
            catch (Exception ex)
            {
                // 记录失败次数不能影响登录接口本身的返回结果
                logger.Warn(ex, $"记录登录失败次数失败：Key={key}");
            }
        }

        /// <summary>
        /// 清除登录失败次数（登录成功或凭据校验通过后调用）
        /// </summary>
        public void ClearLoginFailure(string key)
        {
            key = NormalizeLoginKey(key);
            if (key.Length == 0) return;

            try
            {
                CacheService.RemoveLoginFail(key);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"清除登录失败次数失败：Key={key}");
            }
        }

        /// <summary>
        /// 账号标识归一化：去空格并转小写。
        /// 登录查询对用户名大小写不敏感（数据库排序规则），锁定键若区分大小写，
        /// 攻击者换大小写即可绕过锁定。
        /// </summary>
        private static string NormalizeLoginKey(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// 读取登录失败锁定策略：sys.account.maxRetryCount（默认 5 次）、sys.account.lockTime（默认 10 分钟）
        /// </summary>
        private (int maxRetryCount, int lockMinutes) GetLoginLockOptions()
        {
            int maxRetryCount = 5;
            int lockMinutes = 10;

            try
            {
                var retryConfig = sysConfigService.GetSysConfigByKey("sys.account.maxRetryCount");
                if (retryConfig != null && int.TryParse(retryConfig.ConfigValue, out var retry)) maxRetryCount = retry;

                var lockConfig = sysConfigService.GetSysConfigByKey("sys.account.lockTime");
                if (lockConfig != null && int.TryParse(lockConfig.ConfigValue, out var lockTime)) lockMinutes = lockTime;
            }
            catch (Exception ex)
            {
                // 配置读不到按默认策略走：锁定是安全兜底，不能因配置库抖动而失效
                logger.Warn(ex, "读取登录失败锁定配置失败，使用默认策略");
            }

            return (maxRetryCount, lockMinutes);
        }

        /// <summary>
        /// 锁定提醒：登录态尚未建立，使用 ClearFilter 跨数据权限解析用户后发送系统消息。
        /// 按用户名查不到时（短信登录传的是手机号）再按手机号查一次。
        /// </summary>
        private void NotifyUserLocked(string key, int minutes)
        {
            try
            {
                var userId = Context.Queryable<SysUser>().ClearFilter()
                    .Where(u => u.UserName == key)
                    .Select(u => u.UserId).First();
                if (userId <= 0)
                {
                    userId = Context.Queryable<SysUser>().ClearFilter()
                        .Where(u => u.Phonenumber == key)
                        .Select(u => u.UserId).First();
                }
                if (userId > 0)
                {
                    sysUserMsgService.AddSysUserMsg(userId, $"您的账号因多次登录失败已被锁定，剩余{minutes}分钟，解锁后请重试", UserMsgType.SYSTEM);
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"账号锁定提醒发送失败：Key={key}");
            }
        }

        public List<StatiLoginLogDto> GetStatiLoginlog()
        {
            var db = ResolveTenantDb();
            var time = DateTime.Now;

            //如果是查询当月那么 time就是 DateTime.Now
            var days = (time.AddMonths(1) - time).Days;//获取当月天数
            var dayArray = Enumerable.Range(1, days).Select(it => Convert.ToDateTime(time.ToString("yyyy-MM-" + it))).ToList();//转成时间数组

            var queryableLeft = db.Reportable(dayArray)
                .ToQueryable<DateTime>();

            var queryableRight = db.Queryable<SysLogininfor>().ApplyScope();
            var list = db.Queryable(queryableLeft, queryableRight, JoinType.Left, (x1, x2)
                 => x2.LoginTime.ToString("yyyy-MM-dd") == x1.ColumnName.ToString("yyyy-MM-dd"))
                .GroupBy((x1, x2) => x1.ColumnName)
                .Where((x1, x2) => x1.ColumnName >= DateTime.Now.AddDays(-7) && x1.ColumnName <= DateTime.Now)
                .Select((x1, x2) => new StatiLoginLogDto()
                {
                    DeRepeatNum = SqlFunc.AggregateDistinctCount(x2.Ipaddr),
                    Num = SqlFunc.AggregateCount(x2.InfoId),
                    Date = x1.ColumnName,
                })
                .Mapper(it =>
                {
                    it.WeekName = Tools.GetWeekByDate(it.Date);//相当于ToList循环赋值
                }).ToList();
            return list;
        }

        /// <summary>
        /// 聚合登录日志安全指标供 AI 解读。走租户库 + 数据范围过滤，与登录日志列表同一口径；
        /// 指标由固定 SQL 聚合，AI 只解读结果，不接触原始日志也不生成任何 SQL。
        /// </summary>
        public LoginSecurityMetricsDto GetLoginSecurityMetrics(LogAiAnalysisInput input)
        {
            var (begin, end) = input.ResolveRange();
            var metrics = new LoginSecurityMetricsDto
            {
                TimeRange = $"{begin:yyyy-MM-dd} ~ {end:yyyy-MM-dd}"
            };

            var db = ResolveTenantDb();

            // 成功/失败总量（sys_logininfor.status：0成功 1失败）
            foreach (var row in BuildRangeQuery(db, begin, end).GroupBy(it => it.Status)
                .Select(it => new { Status = it.Status, Num = SqlFunc.AggregateCount(it.InfoId) })
                .ToList())
            {
                if (row.Status == "0")
                {
                    metrics.SuccessCount = row.Num;
                }
                else if (row.Status == "1")
                {
                    metrics.FailCount = row.Num;
                }
            }
            metrics.TotalCount = metrics.SuccessCount + metrics.FailCount;

            // 每日成功/失败趋势
            metrics.Daily = BuildRangeQuery(db, begin, end).GroupBy(it => it.LoginTime.ToString("yyyy-MM-dd"))
                .Select(it => new
                {
                    Date = it.LoginTime.ToString("yyyy-MM-dd"),
                    Success = SqlFunc.AggregateSum(SqlFunc.IIF(it.Status == "0", 1, 0)),
                    Fail = SqlFunc.AggregateSum(SqlFunc.IIF(it.Status == "1", 1, 0))
                })
                .ToList()
                .OrderBy(x => x.Date)
                .Select(x => new LoginDailyStat { Date = x.Date, Success = x.Success, Fail = x.Fail })
                .ToList();

            // 凌晨 0-6 点活跃（异常时段信号）
            metrics.NightCount = BuildRangeQuery(db, begin, end).Where(it => it.LoginTime.Hour < 6).Count();
            metrics.NightFailCount = BuildRangeQuery(db, begin, end).Where(it => it.LoginTime.Hour < 6 && it.Status == "1").Count();

            // 失败账号 Top：按账号+地点分组后在内存归并，才能拿到地点去重数（异地/共享账号信号）
            var failRows = BuildRangeQuery(db, begin, end).Where(it => it.Status == "1")
                .GroupBy(it => new { it.UserName, it.LoginLocation })
                .Select(it => new
                {
                    it.UserName,
                    it.LoginLocation,
                    Num = SqlFunc.AggregateCount(it.InfoId),
                    Last = SqlFunc.AggregateMax(it.LoginTime)
                })
                .ToList();
            metrics.FailedAccounts = failRows
                .GroupBy(x => x.UserName)
                .Select(g => new LoginFailAccountStat
                {
                    UserName = g.Key,
                    FailCount = g.Sum(x => x.Num),
                    LocationCount = g.Select(x => x.LoginLocation).Where(x => !string.IsNullOrEmpty(x)).Distinct().Count(),
                    Locations = string.Join("、", g.Select(x => x.LoginLocation).Where(x => !string.IsNullOrEmpty(x)).Distinct().Take(5)),
                    LastFailTime = g.Max(x => x.Last)
                })
                .OrderByDescending(x => x.FailCount)
                .Take(10)
                .ToList();

            // 失败 IP Top
            metrics.FailedIps = BuildRangeQuery(db, begin, end).Where(it => it.Status == "1")
                .GroupBy(it => new { it.Ipaddr, it.LoginLocation })
                .Select(it => new
                {
                    it.Ipaddr,
                    it.LoginLocation,
                    Num = SqlFunc.AggregateCount(it.InfoId),
                    Last = SqlFunc.AggregateMax(it.LoginTime)
                })
                .ToList()
                .GroupBy(x => x.Ipaddr)
                .Select(g => new LoginFailIpStat
                {
                    Ipaddr = g.Key,
                    FailCount = g.Sum(x => x.Num),
                    Location = g.Select(x => x.LoginLocation).FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? string.Empty,
                    LastFailTime = g.Max(x => x.Last)
                })
                .OrderByDescending(x => x.FailCount)
                .Take(10)
                .ToList();

            // 客户端环境分布 Top5
            metrics.Browsers = BuildRangeQuery(db, begin, end).GroupBy(it => it.Browser)
                .Select(it => new { Name = it.Browser, Num = SqlFunc.AggregateCount(it.InfoId) })
                .ToList()
                .OrderByDescending(x => x.Num).Take(5)
                .Select(x => new NameCountStat { Name = x.Name, Count = x.Num })
                .ToList();
            metrics.Oses = BuildRangeQuery(db, begin, end).GroupBy(it => it.Os)
                .Select(it => new { Name = it.Os, Num = SqlFunc.AggregateCount(it.InfoId) })
                .ToList()
                .OrderByDescending(x => x.Num).Take(5)
                .Select(x => new NameCountStat { Name = x.Name, Count = x.Num })
                .ToList();

            // IP 面貌：区间去重 IP 与近 30 天未出现的"新 IP"
            var rangeIps = BuildRangeQuery(db, begin, end)
                .Where(it => it.Ipaddr != null && it.Ipaddr != "")
                .Select(it => it.Ipaddr).Distinct().ToList();
            metrics.DistinctIpCount = rangeIps.Count;
            var priorIps = db.Queryable<SysLogininfor>().ApplyScope()
                .Where(it => it.LoginTime >= begin.AddDays(-30) && it.LoginTime < begin && it.Ipaddr != null && it.Ipaddr != "")
                .Select(it => it.Ipaddr).Distinct().ToList()
                .ToHashSet(StringComparer.Ordinal);
            metrics.NewIpCount = rangeIps.Count(x => !priorIps.Contains(x));

            // 与登录日志列表同口径：无"真实 IP 查看"敏感权限时 IP 脱敏后再喂模型/展示，
            // 避免无权限用户绕过列表页拿到明文 IP
            if (!HttpContextExtension.HasSensitivePerm(App.HttpContext, SensitivePerms.ViewRealIP))
            {
                foreach (var ip in metrics.FailedIps)
                {
                    ip.Ipaddr = MaskUtil.MaskIp(ip.Ipaddr);
                }
                metrics.IpMasked = true;
            }

            // 成功侧异地信号：同账号多地点 / 相对近30天新地点 / 2小时内跨地切换
            BuildRemoteLoginMetrics(db, metrics, begin, end, input.Limit);

            return metrics;
        }

        /// <summary>
        /// 成功侧异地信号聚合：同账号成功登录多地点、相对近 30 天成功历史出现新地点、2 小时内跨地点切换。
        /// 地点统一按省-市归一化（忽略运营商与未知段），与登录时的异地提醒同一口径。
        /// limit：返回条数上限（1-50，默认15）。
        /// 为避免大范围全量回表，先 SQL 按 账号+地点 粗聚合，内存归并出候选后再对 Top 账号回表做相邻时序检测。
        /// </summary>
        private static void BuildRemoteLoginMetrics(ISqlSugarClient db, LoginSecurityMetricsDto metrics, DateTime begin, DateTime end, int? limit)
        {
            const int rapidSwitchHours = 2;
            var maxAccounts = Math.Clamp(limit ?? 15, 1, 50);

            // 区间内成功登录（sys_logininfor.status：0成功），按 账号+地点 聚合
            var rows = db.Queryable<SysLogininfor>().ApplyScope()
                .Where(it => it.Status == "0" && it.LoginTime >= begin && it.LoginTime <= end
                    && it.UserName != null && it.UserName != "" && it.LoginLocation != null && it.LoginLocation != "")
                .GroupBy(it => new { it.UserName, it.LoginLocation })
                .Select(it => new
                {
                    it.UserName,
                    it.LoginLocation,
                    Num = SqlFunc.AggregateCount(it.InfoId),
                    Last = SqlFunc.AggregateMax(it.LoginTime)
                })
                .ToList();

            // 近 30 天成功历史地点集合（账号"新地点"判定基准，不含本次区间）
            var histRows = db.Queryable<SysLogininfor>().ApplyScope()
                .Where(it => it.Status == "0" && it.LoginTime >= begin.AddDays(-30) && it.LoginTime < begin
                    && it.UserName != null && it.UserName != "")
                .GroupBy(it => new { it.UserName, it.LoginLocation })
                .Select(it => new { it.UserName, it.LoginLocation })
                .ToList()
                .GroupBy(x => x.UserName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => NormalizeLoginLocation(x.LoginLocation)).Where(x => x.Length > 0)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            // 内存归并：每个账号的地点集合与新增地点
            var candidates = new List<RemoteLoginAccountStat>();
            foreach (var g in rows.GroupBy(x => x.UserName, StringComparer.OrdinalIgnoreCase))
            {
                var locs = g.Select(x => NormalizeLoginLocation(x.LoginLocation))
                    .Where(x => x.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                histRows.TryGetValue(g.Key, out var hist);
                var hasHist = hist != null && hist.Count > 0;
                var newLocs = hasHist
                    ? locs.Where(x => !hist.Contains(x)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
                    : new List<string>();
                if (locs.Count < 2 && newLocs.Count == 0)
                {
                    continue; // 无异地信号：单地点且不是相对历史的新地点
                }

                candidates.Add(new RemoteLoginAccountStat
                {
                    UserName = g.Key,
                    SuccessCount = g.Sum(x => x.Num),
                    LocationCount = locs.Count,
                    Locations = string.Join("、", locs.Take(5)),
                    NewLocationCount = newLocs.Count,
                    NewLocations = string.Join("、", newLocs.Take(3)),
                    LastLoginTime = g.Max(x => x.Last)
                });
            }

            metrics.RemoteLoginAccountCount = candidates.Count;
            if (candidates.Count == 0)
            {
                return;
            }

            // 时序检测只对 Top 账号回表：相邻两次成功登录 2 小时内且地点不同记一次跨地切换
            var top = candidates
                .OrderByDescending(x => x.LocationCount + x.NewLocationCount)
                .ThenByDescending(x => x.LastLoginTime)
                .Take(maxAccounts)
                .ToList();
            var names = top.Select(x => x.UserName).ToList();
            var details = db.Queryable<SysLogininfor>().ApplyScope()
                .Where(it => it.Status == "0" && it.LoginTime >= begin && it.LoginTime <= end
                    && names.Contains(it.UserName) && it.LoginLocation != null && it.LoginLocation != "")
                .OrderBy(it => it.UserName).OrderBy(it => it.LoginTime)
                .Select(it => new { it.UserName, it.LoginTime, it.LoginLocation })
                .ToList();

            foreach (var userGroup in details.GroupBy(x => x.UserName, StringComparer.OrdinalIgnoreCase))
            {
                long switchCount = 0;
                string prevLoc = null;
                DateTime? prevTime = null;
                foreach (var row in userGroup.OrderBy(x => x.LoginTime))
                {
                    var loc = NormalizeLoginLocation(row.LoginLocation);
                    if (prevLoc != null && prevTime.HasValue
                        && !string.Equals(loc, prevLoc, StringComparison.OrdinalIgnoreCase)
                        && (row.LoginTime - prevTime.Value).TotalHours < rapidSwitchHours)
                    {
                        switchCount++;
                    }
                    if (loc.Length > 0)
                    {
                        prevLoc = loc;
                        prevTime = row.LoginTime;
                    }
                    else
                    {
                        prevTime = row.LoginTime; // 地点未知行不改变"已知地点"，仅推进时间
                    }
                }
                top.First(x => string.Equals(x.UserName, userGroup.Key, StringComparison.OrdinalIgnoreCase)).RapidSwitchCount = switchCount;
            }

            metrics.RemoteLoginAccounts = top;
        }

        // 地域分布统计的区间上限：省份数有限，允许比按天趋势更长的跨度
        private const int RegionStatsMaxDays = 180;
        private const string UnknownRegionName = "未知";
        private const string OtherRegionName = "其他";

        /// <summary>
        /// 按省份聚合登录日志的地域分布（供 AI 图表问答使用，不返回原始日志）。
        /// 口径与登录日志列表一致：租户库 + 数据范围过滤（非管理员仅统计本人）。
        /// 地域取自登录时 IP 解析写入的 LoginLocation（格式"省-市-运营商"），取首段作为省份。
        /// </summary>
        public List<LoginRegionStat> GetLoginRegionStats(LogAiAnalysisInput input, int topN = 12)
        {
            input ??= new LogAiAnalysisInput();
            var (begin, end) = input.ResolveRange(RegionStatsMaxDays);
            var db = ResolveTenantDb();

            // 按（账号, 地点）粒度聚合：既避免拉取全量明细，又保证"独立用户数"在同省跨市时不重复计数。
            // 若直接按地点分组求 DistinctCount，同省不同市会把同一账号重复计入。
            var rows = BuildRangeQuery(db, begin, end)
                .GroupBy(it => new { it.UserName, it.LoginLocation })
                .Select(it => new
                {
                    UserName = it.UserName,
                    Location = it.LoginLocation,
                    Num = SqlFunc.AggregateCount(it.InfoId)
                })
                .ToList();

            var loginCount = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var users = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var region = ExtractProvince(row.Location);
                loginCount.TryGetValue(region, out var count);
                loginCount[region] = count + row.Num;

                if (!users.TryGetValue(region, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    users[region] = set;
                }
                // 失败尝试可能没有账号，用空串占位，保证这部分登录次数不丢、且不与其他空值混淆
                set.Add(row.UserName ?? string.Empty);
            }

            var stats = loginCount
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => (
                    Region: kv.Key,
                    LoginCount: kv.Value,
                    Users: users.TryGetValue(kv.Key, out var set) ? set : new HashSet<string>()))
                .ToList();

            return MergeTailRegions(stats, topN);
        }

        /// <summary>
        /// 长尾省份合并为"其他"，避免饼图出现几十个碎扇区。
        /// 合并时独立用户数取并集而非求和，保证口径与未合并时一致。
        /// </summary>
        private static List<LoginRegionStat> MergeTailRegions(
            List<(string Region, long LoginCount, HashSet<string> Users)> stats, int topN)
        {
            if (topN <= 0 || stats.Count <= topN)
            {
                return stats.Select(x => ToRegionStat(x)).ToList();
            }

            var result = stats.Take(topN).Select(ToRegionStat).ToList();
            var tail = stats.Skip(topN).ToList();
            if (tail.Count == 0)
            {
                return result;
            }

            var mergedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in tail)
            {
                mergedUsers.UnionWith(item.Users);
            }

            result.Add(new LoginRegionStat
            {
                Region = OtherRegionName,
                LoginCount = tail.Sum(x => x.LoginCount),
                UserCount = mergedUsers.Count
            });
            return result;
        }

        private static LoginRegionStat ToRegionStat((string Region, long LoginCount, HashSet<string> Users) item)
            => new() { Region = item.Region, LoginCount = item.LoginCount, UserCount = item.Users.Count };

        /// <summary>
        /// 从 LoginLocation（"省-市-运营商"）提取省份。
        /// 无法解析（空值或全为占位 0）时归入"未知"，保持与异地登录检测相同的分段规则。
        /// </summary>
        private static string ExtractProvince(string location)
        {
            if (location.IsEmpty())
            {
                return UnknownRegionName;
            }

            foreach (var segment in location.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                // 与 NormalizeLoginLocation 一致：0 是 IP 库未识别时的占位段
                if (string.Equals(segment, "0", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                return segment;
            }

            return UnknownRegionName;
        }

        /// <summary>
        /// 构建区间内登录日志查询（租户库 + 数据范围过滤）。每次调用返回全新 queryable，
        /// 避免 SqlSugar 同一实例在多次聚合之间复用时共享查询状态产生串扰。
        /// </summary>
        private ISugarQueryable<SysLogininfor> BuildRangeQuery(ISqlSugarClient db, DateTime begin, DateTime end)
        {
            return db.Queryable<SysLogininfor>().ApplyScope()
                .Where(it => it.LoginTime >= begin && it.LoginTime <= end);
        }

        /// <summary>
        /// 检查登录是否发生异地，并生成系统消息提醒用户（仅在成功登录时调用）。
        /// </summary>
        /// <param name="user"></param>
        /// <param name="currentLoginIp"></param>
        /// <returns></returns>
        public string GetAbnormalLoginNotice(SysUser user, string currentLoginIp)
        {
            if (user == null || user.UserId <= 0)
            {
                return string.Empty;
            }

            var currentLocation = NormalizeLoginLocation(GetLocationByIp(currentLoginIp));
            var previousLocation = NormalizeLoginLocation(GetLocationByIp(user.LoginIP));

            if (previousLocation.IsEmpty() || currentLocation.IsEmpty())
            {
                return string.Empty;
            }

            if (string.Equals(previousLocation, currentLocation, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var content = $"⚠️ 账号异地登录提醒\n\n检测到您的账号发生异地登录，请确认是否为本人操作：\n\n账号：{user.UserName}\n本次地点：{currentLocation}\n上次地点：{previousLocation}\n登录 IP：{currentLoginIp}\n\n如非本人操作，请立即修改密码！";
            sysUserMsgService.AddSysUserMsg(user.UserId, content, UserMsgType.SYSTEM);
            return content;
        }

        /// <summary>
        /// 登录时预计算合并后的数据权限等级（取所有角色中最宽松的权限）
        /// </summary>
        public static int ComputeScopeType(List<SysRole> roles)
        {
            if (roles.Any(r => r.RoleKey == GlobalConstant.AdminRole || r.DataScope == (int)DataPermiEnum.All))
                return (int)MergedScopeType.All;
            if (roles.Any(r => r.DataScope == (int)DataPermiEnum.DEPT_CHILD || r.DataScope == (int)DataPermiEnum.CUSTOM))
                return (int)MergedScopeType.DeptList;
            if (roles.Any(r => r.DataScope == (int)DataPermiEnum.DEPT))
                return (int)MergedScopeType.Dept;
            if (roles.Any(r => r.DataScope == (int)DataPermiEnum.SELF))
                return (int)MergedScopeType.Self;
            return (int)MergedScopeType.None;
        }

        private static string GetLocationByIp(string ip)
        {
            if (ip.IsEmpty())
            {
                return string.Empty;
            }

            var ipInfo = IpTool.Search(ip);
            return $"{ipInfo?.Province}-{ipInfo?.City}-{ipInfo?.NetworkOperator}";
        }

        private static string NormalizeLoginLocation(string location)
        {
            if (location.IsEmpty())
            {
                return string.Empty;
            }

            var segments = location
                .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(segment => !segment.Equals("0", StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToArray();

            return segments.Length == 0 ? string.Empty : string.Join("-", segments);
        }

    }
}
