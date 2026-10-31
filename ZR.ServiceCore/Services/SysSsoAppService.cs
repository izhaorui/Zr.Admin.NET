using Infrastructure;
using Infrastructure.Attribute;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Repository;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// SSO 应用接入管理
    /// </summary>
    [AppService(ServiceType = typeof(ISysSsoAppService), ServiceLifetime = LifeTime.Transient)]
    public class SysSsoAppService : BaseService<SysSsoApp>, ISysSsoAppService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 允许下发的扩展字段白名单。未在清单内的值一律丢弃，避免管理端误填后把字段带出去。
        /// profile = 昵称/头像/部门；email、phone 属个人信息，单独授权。
        /// </summary>
        private static readonly string[] AllowedFieldsPool = { "profile", "email", "phone" };

        public PagedInfo<SysSsoApp> GetPageList(SsoAppQueryDto parm)
        {
            var predicate = Expressionable.Create<SysSsoApp>();
            predicate = predicate.AndIF(parm.AppId.IfNotEmpty(), it => it.AppId.Contains(parm.AppId));
            predicate = predicate.AndIF(parm.AppName.IfNotEmpty(), it => it.AppName.Contains(parm.AppName));
            predicate = predicate.AndIF(parm.Status.HasValue, it => it.Status == parm.Status.Value);

            var page = Queryable()
                .Where(predicate.ToExpression())
                .OrderBy(it => it.Id, OrderByType.Desc)
                .ToPage(parm);

            // 密钥哈希不返回给前端，任何页面都不需要它
            page.Result?.ForEach(it => it.AppSecretHash = null);
            return page;
        }

        public SsoAppSecretDto CreateApp(SsoAppEditDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.AppName))
            {
                throw new CustomException("应用名称不能为空");
            }

            List<string> redirectUris = NormalizeRedirectUris(dto.RedirectUris);
            if (redirectUris.Count == 0)
            {
                throw new CustomException("至少配置一个回调地址");
            }

            string appId = "sso_" + Guid.NewGuid().ToString("N")[..12];
            string secret = GenerateSecret();

            var app = new SysSsoApp
            {
                AppId = appId,
                AppSecretHash = HashSecret(appId, secret),
                AppName = dto.AppName.Trim(),
                RedirectUris = string.Join("\n", redirectUris),
                AllowFields = NormalizeAllowFields(dto.AllowFields),
                Status = dto.Status,
                ExpireTime = dto.ExpireTime,
                Remark = dto.Remark
            };

            Add(app);
            logger.Info($"SSO应用创建：AppId={appId}, AppName={app.AppName}");

            // Add 走 ExecuteCommand 不回填自增主键，这里回查一次拿 Id 返给管理端
            var saved = GetByAppId(appId);
            return new SsoAppSecretDto { Id = saved?.Id ?? 0, AppId = appId, AppSecret = secret };
        }

        public int UpdateApp(SsoAppEditDto dto)
        {
            if (dto == null || dto.Id <= 0)
            {
                throw new CustomException("参数错误");
            }

            var app = GetId(dto.Id);
            if (app == null)
            {
                throw new CustomException("应用不存在");
            }

            if (string.IsNullOrWhiteSpace(dto.AppName))
            {
                throw new CustomException("应用名称不能为空");
            }

            List<string> redirectUris = NormalizeRedirectUris(dto.RedirectUris);
            if (redirectUris.Count == 0)
            {
                throw new CustomException("至少配置一个回调地址");
            }

            app.AppName = dto.AppName.Trim();
            app.RedirectUris = string.Join("\n", redirectUris);
            app.AllowFields = NormalizeAllowFields(dto.AllowFields);
            app.Status = dto.Status;
            app.ExpireTime = dto.ExpireTime;
            app.Remark = dto.Remark;

            // ignoreNullColumns: false —— 允许把过期时间、备注清空
            return Update(app, false);
        }

        public SsoAppSecretDto ResetSecret(long id)
        {
            var app = GetId(id);
            if (app == null)
            {
                throw new CustomException("应用不存在");
            }

            string secret = GenerateSecret();
            app.AppSecretHash = HashSecret(app.AppId, secret);
            app.Update_time = DateTime.Now;

            // 只更新密钥列，避免把并发修改的其他字段覆盖回旧值
            Update(app, it => new { it.AppSecretHash, it.Update_time });

            logger.Info($"SSO应用密钥重置：AppId={app.AppId}");
            return new SsoAppSecretDto { Id = app.Id, AppId = app.AppId, AppSecret = secret };
        }

        public SysSsoApp GetByAppId(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return null;
            }

            string id = appId.Trim();
            return GetFirst(it => it.AppId == id);
        }

        public SysSsoApp ValidateApp(string appId, string appSecret = null)
        {
            var app = GetByAppId(appId);
            if (app == null || app.Status != 0)
            {
                return null;
            }

            if (app.ExpireTime.HasValue && app.ExpireTime.Value < DateTime.Now)
            {
                return null;
            }

            if (appSecret != null && !VerifySecret(app, appSecret))
            {
                return null;
            }

            return app;
        }

        public bool IsRedirectUriAllowed(SysSsoApp app, string redirectUri)
        {
            if (app == null || string.IsNullOrWhiteSpace(redirectUri))
            {
                return false;
            }

            string target = redirectUri.Trim();

            // 必须精确全等：协议/域名/端口/路径/查询串任一不同都不放行。
            // 用 StartsWith/Contains 会被 https://good.com.attacker.com 这类地址绕过。
            foreach (string item in SplitRedirectUris(app.RedirectUris))
            {
                if (string.Equals(item, target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public string HashSecret(string appId, string secret)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{appId}:{secret}")));
        }

        private bool VerifySecret(SysSsoApp app, string secret)
        {
            if (string.IsNullOrEmpty(secret))
            {
                return false;
            }

            byte[] actual = Encoding.UTF8.GetBytes(HashSecret(app.AppId, secret));
            byte[] expected = Encoding.UTF8.GetBytes(app.AppSecretHash ?? string.Empty);

            // 定长比较，避免按字节提前返回泄露哈希前缀
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        private static string GenerateSecret()
        {
            // 24 字节 → 48 位十六进制，足够抵抗离线爆破
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        }

        private static List<string> NormalizeRedirectUris(string redirectUris)
        {
            if (string.IsNullOrWhiteSpace(redirectUris))
            {
                return new List<string>();
            }

            return SplitRedirectUris(redirectUris).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static IEnumerable<string> SplitRedirectUris(string redirectUris)
        {
            if (string.IsNullOrWhiteSpace(redirectUris))
            {
                yield break;
            }

            foreach (string line in redirectUris.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string item = line.Trim();
                if (item.Length == 0)
                {
                    continue;
                }

                // 只接受绝对地址：相对地址无法判断回跳域名，等于允许被拼到任意站点
                if (!Uri.TryCreate(item, UriKind.Absolute, out Uri uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new CustomException($"回调地址[{item}]格式不正确，必须是 http/https 绝对地址");
                }

                // 带 fragment 的地址会把 ?code= 拼到 # 之后，授权码到不了服务端
                if (item.Contains('#'))
                {
                    throw new CustomException($"回调地址[{item}]不能包含 # 片段，请去掉后重试");
                }

                yield return item;
            }
        }

        private static string NormalizeAllowFields(string allowFields)
        {
            if (string.IsNullOrWhiteSpace(allowFields))
            {
                return string.Empty;
            }

            List<string> fields = allowFields
                .Split(new[] { ',', '，', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(it => it.Trim().ToLowerInvariant())
                .Where(it => AllowedFieldsPool.Contains(it))
                .Distinct()
                .ToList();

            return string.Join(",", fields);
        }
    }
}