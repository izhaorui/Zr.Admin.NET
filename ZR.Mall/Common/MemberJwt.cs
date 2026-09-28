using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using ZR.Model.Member;

namespace ZR.Mall.Common
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
        private const string SecretSuffix = "-MEMBER-CHANNEL";
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
        /// 独立密钥：MemberJwt:SecretKey 优先；未配置时由后台密钥派生，保证与后台密钥必然不同（双向不可互换）
        /// </summary>
        private static string GetSecretKey()
        {
            var key = App.Configuration["MemberJwt:SecretKey"];
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key;
            }
            var baseKey = App.Configuration["JwtSettings:SecretKey"] ?? "ZRAdmin.Member.Default.Secret.Key";
            return baseKey + SecretSuffix;
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
