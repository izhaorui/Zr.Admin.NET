using Infrastructure;
using Infrastructure.Attribute;
using System.Linq;
using ZR.Infrastructure.Helper;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Model.System.Enums;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 对外 SSO 登录授权。
    ///
    /// 本服务继承 BaseService&lt;SysSsoApp&gt;，Context 固定为主库（SysSsoApp 是 IMainDbEntity），
    /// 用于在匿名上下文中按 AppId 查应用；查询用户时再显式切到授权码载荷中的租户库。
    /// </summary>
    [AppService(ServiceType = typeof(ISsoService), ServiceLifetime = LifeTime.Transient)]
    public class SsoService : BaseService<SysSsoApp>, ISsoService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>授权码有效期（秒）</summary>
        public const int SsoCodeTtlSeconds = 60;

        /// <summary>同一应用每分钟允许的兑换失败次数，超过即拒绝，用于挡密钥爆破</summary>
        private const int TokenFailLimitPerMinute = 20;

        private readonly ISysSsoAppService ssoAppService;
        private readonly ISysTenantService sysTenantService;

        public SsoService(ISysSsoAppService ssoAppService, ISysTenantService sysTenantService)
        {
            this.ssoAppService = ssoAppService;
            this.sysTenantService = sysTenantService;
        }

        public SsoAuthorizeInfoDto ValidateAuthorize(string appId, string redirectUri, string state)
        {
            var app = ssoAppService.ValidateApp(appId);
            if (app == null)
            {
                throw new CustomException("应用不存在或已停用");
            }

            if (!ssoAppService.IsRedirectUriAllowed(app, redirectUri))
            {
                throw new CustomException("回调地址未授权");
            }

            return new SsoAuthorizeInfoDto
            {
                AppId = app.AppId,
                AppName = app.AppName,
                RedirectUri = redirectUri.Trim(),
                State = state
            };
        }

        public SsoCodeResultDto IssueCode(long userId, SsoCodeRequestDto dto)
        {
            if (userId <= 0)
            {
                throw new CustomException("请先登录");
            }

            if (dto == null || string.IsNullOrWhiteSpace(dto.AppId))
            {
                throw new CustomException("缺少应用标识");
            }

            var app = ssoAppService.ValidateApp(dto.AppId);
            if (app == null)
            {
                throw new CustomException("应用不存在或已停用");
            }

            if (!ssoAppService.IsRedirectUriAllowed(app, dto.RedirectUri))
            {
                throw new CustomException("回调地址未授权");
            }

            string tenantId = App.GetCurrentTenantId();
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                tenantId = App.SystemDbConfigId;
            }

            string code = Guid.NewGuid().ToString("N");
            CacheService.SetSsoCode(code, new SsoCodePayload
            {
                AppId = app.AppId,
                RedirectUri = dto.RedirectUri.Trim(),
                UserId = userId,
                TenantId = tenantId
            }, SsoCodeTtlSeconds);

            logger.Info($"SSO授权码签发：AppId={app.AppId}, UserId={userId}, TenantId={tenantId}");

            return new SsoCodeResultDto
            {
                Code = code,
                ExpiresIn = SsoCodeTtlSeconds,
                RedirectUri = BuildRedirectUrl(dto.RedirectUri.Trim(), code, dto.State),
                State = dto.State
            };
        }

        public SsoUserInfoDto ExchangeCode(SsoTokenRequestDto dto, string clientIp)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.AppId)
                || string.IsNullOrWhiteSpace(dto.AppSecret) || string.IsNullOrWhiteSpace(dto.Code))
            {
                throw new CustomException("参数不完整");
            }

            string maskedIp = MaskUtil.MaskIp(clientIp);

            // 计数键与校验值必须用同一个规范化后的 AppId，
            // 否则在 id 前后加空格即可换出一个新计数键，绕过限流
            string appId = dto.AppId.Trim();

            // 先只读判断历史失败次数：已超限直接拒绝，不再查库。
            // 只在失败时累加，成功兑换不计入，避免同一应用并发合法请求互相挤占额度。
            if (CacheService.GetSsoTokenFail(appId) >= TokenFailLimitPerMinute)
            {
                logger.Warn($"SSO兑换被限流：AppId={appId}, Ip={maskedIp}");
                throw new CustomException("请求过于频繁，请稍后再试");
            }

            var app = ssoAppService.ValidateApp(appId, dto.AppSecret);
            if (app == null)
            {
                CacheService.IncreaseSsoTokenFail(appId);
                // 不区分"应用不存在/密钥错误/已停用"，避免给爆破方提供判断依据
                logger.Warn($"SSO兑换失败（应用校验不通过）：AppId={appId}, Ip={maskedIp}");
                throw new CustomException("应用校验失败");
            }

            var payload = CacheService.ConsumeSsoCode<SsoCodePayload>(dto.Code.Trim(), SsoCodeTtlSeconds);
            if (payload == null)
            {
                logger.Warn($"SSO兑换失败（授权码无效或已使用）：AppId={app.AppId}, Ip={maskedIp}");
                throw new CustomException("授权码无效或已过期");
            }

            // 授权码签发时已绑定应用，这里再比一次：防止 A 应用的授权码被拿到 B 应用兑换
            if (!string.Equals(payload.AppId, app.AppId, StringComparison.Ordinal))
            {
                logger.Warn($"SSO兑换失败（授权码归属不符）：CodeAppId={payload.AppId}, RequestAppId={app.AppId}, Ip={maskedIp}");
                throw new CustomException("授权码无效或已过期");
            }

            // 用户登录后被停服/注销时不再放行，避免"登录一次、长期换身份"
            if (!sysTenantService.IsTenantActive(payload.TenantId))
            {
                logger.Warn($"SSO兑换失败（租户不可用）：AppId={app.AppId}, TenantId={payload.TenantId}");
                throw new CustomException("所属租户不可用");
            }

            var db = ResolveTenantDb(payload.TenantId);
            var user = db.Queryable<SysUser>().ClearFilter()
                .Where(it => it.UserId == payload.UserId)
                .Select(it => new
                {
                    it.UserId,
                    it.UserName,
                    it.NickName,
                    it.Avatar,
                    it.Email,
                    it.Phonenumber,
                    it.DeptId,
                    it.Status,
                    it.DelFlag
                })
                .First();

            if (user == null || user.Status == 1 || user.DelFlag == DelFlagEnum.Deleted)
            {
                logger.Warn($"SSO兑换失败（用户不可用）：AppId={app.AppId}, UserId={payload.UserId}, TenantId={payload.TenantId}");
                throw new CustomException("用户不存在或已停用");
            }

            var fields = ParseAllowFields(app.AllowFields);
            var info = new SsoUserInfoDto
            {
                UserId = user.UserId,
                UserName = user.UserName,
                TenantId = payload.TenantId
            };

            // profile = 基础资料：昵称、头像、部门。
            // 这三项只用于展示，合并成一个授权项，避免管理员在"给不给头像"这类无决策价值的项上纠结。
            if (fields.Contains("profile"))
            {
                info.NickName = user.NickName;
                info.Avatar = user.Avatar;
                info.DeptId = user.DeptId;
                if (user.DeptId > 0)
                {
                    info.DeptName = db.Queryable<SysDept>().ClearFilter()
                        .Where(it => it.DeptId == user.DeptId)
                        .Select(it => it.DeptName)
                        .First();
                }
            }

            // 邮箱、手机号属可识别的个人信息，比头像部门敏感，各自独立授权
            if (fields.Contains("email"))
            {
                info.Email = user.Email;
            }

            if (fields.Contains("phone"))
            {
                info.Phone = user.Phonenumber;
            }

            // 键必须与校验前同一规范化后的 AppId 一致，否则大小写变体会残留计数
            CacheService.ClearSsoTokenFail(appId);
            logger.Info($"SSO兑换成功：AppId={app.AppId}, UserId={user.UserId}, TenantId={payload.TenantId}, Ip={maskedIp}");

            return info;
        }

        private static HashSet<string> ParseAllowFields(string allowFields)
        {
            if (string.IsNullOrWhiteSpace(allowFields))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return allowFields
                .Split(new[] { ',', '，', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(it => it.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static string BuildRedirectUrl(string redirectUri, string code, string state)
        {
            string separator = redirectUri.Contains('?') ? "&" : "?";
            string url = $"{redirectUri}{separator}code={Uri.EscapeDataString(code)}";

            if (!string.IsNullOrEmpty(state))
            {
                url += $"&state={Uri.EscapeDataString(state)}";
            }

            return url;
        }
    }
}