using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using ZR.Model.Member;

namespace ZR.ServiceCore.Member
{
    /// <summary>
    /// C 端会员令牌载荷（仅放非敏感标识，不放昵称/头像等易变数据）
    /// </summary>
    public class MemberToken
    {
        /// <summary>
        /// 会员Id
        /// </summary>
        public long MemberId { get; set; }

        /// <summary>
        /// 手机号
        /// </summary>
        public string Phone { get; set; }

        /// <summary>
        /// 签发时所属租户
        /// </summary>
        public string TenantId { get; set; }
    }

    /// <summary>
    /// 会员令牌签发/校验工具。
    /// 使用独立密钥与 Issuer/Audience，与后台 JwtSettings 双向隔离：
    /// 后台 JwtUtil 无法校验会员令牌，本工具也无法校验后台令牌。
    /// 会员身份为平台级通用能力（与 sys_user 对等），故置于框架共享层 ZR.ServiceCore，
    /// 供商城/内容/社交等业务模块共用，各模块无需引用商城工程。
    /// </summary>
    public static class MemberJwt
    {
        /// <summary>
        /// 会员令牌请求头（避开后台 Authorization，避免两套令牌互相干扰）
        /// </summary>
        public const string TokenHeader = "mall-token";

        private const string ClaimKey = "MemberData";
        private const string DefaultIssuer = "ZRAdmin.Member";
        private const string DefaultAudience = "ZRAdmin.Member.App";
        private const int DefaultExpireMinutes = 20160; // 14 天

        /// <summary>
        /// 为会员签发令牌
        /// </summary>
        public static string GenerateToken(UmsMember member)
        {
            var payload = new MemberToken
            {
                MemberId = member.Id,
                Phone = member.Phone,
                TenantId = App.GetCurrentTenantId()
            };

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, member.Id.ToString()),
                new(ClaimTypes.MobilePhone, member.Phone ?? string.Empty),
                new(ClaimKey, JsonConvert.SerializeObject(payload))
            };

            var now = DateTime.UtcNow;
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = GetIssuer(),
                Audience = GetAudience(),
                IssuedAt = now,
                Expires = now.AddMinutes(GetExpireMinutes()),
                SigningCredentials = new SigningCredentials(GetSecurityKey(), SecurityAlgorithms.HmacSha256Signature)
            };

            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityTokenHandler().CreateToken(descriptor));
        }

        /// <summary>
        /// 从当前请求头解析会员身份；无效/缺失返回 null
        /// </summary>
        public static MemberToken GetMember(HttpContext context)
        {
            var token = context?.Request?.Headers[TokenHeader].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }
            token = token.Replace("Bearer ", string.Empty).Trim();

            // fail-closed：密钥未配置时拒绝识别任何会员身份（上层 MemberAuthorize 返回 OAUTH_FAIL），
            // 这里不抛异常，避免把"未配置"变成所有会员接口的 500。
            if (string.IsNullOrWhiteSpace(App.Configuration["MemberJwt:SecretKey"]))
            {
                NLog.LogManager.GetCurrentClassLogger().Error(
                    "MemberJwt:SecretKey 未配置，已拒绝解析 mall-token。请通过环境配置文件或环境变量 MemberJwt__SecretKey 注入。");
                return null;
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(token, GetValidationParameters(), out _);
                var data = principal.Claims.FirstOrDefault(x => x.Type == ClaimKey)?.Value;
                return string.IsNullOrWhiteSpace(data) ? null : JsonConvert.DeserializeObject<MemberToken>(data);
            }
            catch (Exception ex)
            {
                // 令牌非法/过期属预期情况，仅调试级记录，不打印令牌本身
                NLog.LogManager.GetCurrentClassLogger().Debug(ex, "会员令牌校验失败");
                return null;
            }
        }

        /// <summary>
        /// 令牌有效期（秒），供登录接口返回给客户端
        /// </summary>
        public static int GetExpireSeconds()
        {
            return GetExpireMinutes() * 60;
        }

        private static int GetExpireMinutes()
        {
            return int.TryParse(App.Configuration["MemberJwt:Expire"], out var minutes) && minutes > 0
                ? minutes
                : DefaultExpireMinutes;
        }

        private static string GetIssuer()
        {
            return App.Configuration["MemberJwt:Issuer"] ?? DefaultIssuer;
        }

        private static string GetAudience()
        {
            return App.Configuration["MemberJwt:Audience"] ?? DefaultAudience;
        }

        /// <summary>
        /// 会员令牌专用密钥：只认 MemberJwt:SecretKey，必须显式配置。
        /// 不再从后台 JwtSettings:SecretKey 派生 —— 派生后缀是源码里的公开常量，
        /// 一旦 MemberJwt 未配置就会退化成"可由仓库公开值推算"的弱密钥，可被用来伪造会员身份。
        /// </summary>
        private static string GetSecretKey()
        {
            var key = App.Configuration["MemberJwt:SecretKey"];
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException(
                    "MemberJwt:SecretKey 未配置，无法签发/校验会员令牌。" +
                    "请通过 appsettings.Development.json / appsettings.Production.json（均已被 gitignore）或环境变量 MemberJwt__SecretKey 注入。");
            }
            return key;
        }

        private static SymmetricSecurityKey GetSecurityKey()
        {
            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetSecretKey()));
        }

        private static TokenValidationParameters GetValidationParameters()
        {
            return new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidIssuer = GetIssuer(),
                ValidAudience = GetAudience(),
                IssuerSigningKey = GetSecurityKey(),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        }
    }
}
