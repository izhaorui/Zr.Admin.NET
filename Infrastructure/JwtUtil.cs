using Infrastructure.Extensions;
using Infrastructure.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace Infrastructure
{
    /// <summary>
    /// 2023-8-29已从WebApi移至此
    /// </summary>
    public class JwtUtil
    {
        /// <summary>
        /// 获取用户身份信息
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        public static LoginUser GetLoginUser(HttpContext httpContext)
        {
            string token = httpContext.GetToken();

            if (string.IsNullOrEmpty(token)) return null;

            var tokenModel = ValidateJwtToken(ParseToken(token));
            return tokenModel;
        }

        /// <summary>
        /// 生成token
        /// </summary>
        /// <param name="claims"></param>
        /// <returns></returns>
        public static string GenerateJwtToken(List<Claim> claims)
        {
            JwtSettings jwtSettings = new();
            AppSettings.Bind("JwtSettings", jwtSettings);

            var authTime = DateTime.UtcNow;
            var expiresAt = authTime.AddMinutes(jwtSettings.Expire);
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(jwtSettings.SecretKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = jwtSettings.Issuer,
                Audience = jwtSettings.Audience,
                IssuedAt = authTime,//token生成时间
                Expires = expiresAt,
                //NotBefore = authTime,
                TokenType = jwtSettings.TokenType,
                //对称秘钥，签名证书
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
        /// <summary>
        /// 尝试获取后台令牌校验参数；JwtSettings 未配置（SecretKey 为空）时返回 null，不抛异常。
        /// 用于"不校验后台令牌"的宿主（如公网宿主 ZR.Front.WebApi）的优雅降级路径：
        /// 解析不到参数即视为"没有有效的后台令牌"，而不是让异常冒泡成 500。
        /// </summary>
        public static TokenValidationParameters? TryGetValidParameters()
        {
            JwtSettings jwtSettings = new();
            AppSettings.Bind("JwtSettings", jwtSettings);

            if (jwtSettings == null || jwtSettings.SecretKey.IsEmpty())
            {
                return null;
            }
            var key = Encoding.ASCII.GetBytes(jwtSettings.SecretKey);

            var tokenDescriptor = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateLifetime = true,//是否验证Token有效期，使用当前时间与Token的Claims中的NotBefore和Expires对比
                ClockSkew = TimeSpan.FromSeconds(30)
                //RequireExpirationTime = true,//过期时间
            };
            return tokenDescriptor;
        }

        /// <summary>
        /// 验证Token（严格版）：未配置时抛异常。
        /// 供 AddJwtBearer 等必须在启动阶段确定校验参数的场景使用（见 JwtExtension.AddJwtSetup）。
        /// 仅需"能解析就解析"的场景请用 TryGetValidParameters。
        /// </summary>
        public static TokenValidationParameters ValidParameters()
        {
            return TryGetValidParameters() ?? throw new Exception("JwtSettings获取失败");
        }

        /// <summary>
        /// 从令牌中获取数据声明
        /// </summary>
        /// <param name="token">令牌</param>
        /// <returns></returns>
        public static JwtSecurityToken? ParseToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            // 优雅降级：未配置后台 JwtSettings（如公网宿主）时拿不到校验参数，
            // 视为"没有有效的后台令牌"直接返回 null，避免 JwtSettings获取失败 冒泡成 500。
            var validateParameter = TryGetValidParameters();
            if (validateParameter == null) return null;

            token = token.Replace("Bearer ", "");
            try
            {
                tokenHandler.ValidateToken(token, validateParameter, out SecurityToken validatedToken);

                return tokenHandler.ReadJwtToken(token);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                // return null if validation fails
                return null;
            }
        }

        /// <summary>
        /// jwt token校验
        /// </summary>
        /// <param name="jwtSecurityToken"></param>
        /// <returns></returns>
        public static LoginUser? ValidateJwtToken(JwtSecurityToken jwtSecurityToken)
        {
            try
            {
                if (jwtSecurityToken == null) return null;
                IEnumerable<Claim> claims = jwtSecurityToken?.Claims;
                LoginUser loginUser = null;

                var userData = claims.FirstOrDefault(x => x.Type == ClaimTypes.UserData)?.Value;
                if (userData != null)
                {
                    loginUser = JsonConvert.DeserializeObject<LoginUser>(userData);
                    loginUser.ExpireTime = jwtSecurityToken.ValidTo;
                }
                return loginUser;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        /// <summary>
        ///组装Claims
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static List<Claim> AddClaims(LoginUser user)
        {
            var claims = new List<Claim>()
                {
                    new(ClaimTypes.PrimarySid, user.UserId.ToString()),
                    new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new(ClaimTypes.Name, user.UserName),
                    new(ClaimTypes.GroupSid, user.DeptId.ToString()),
                    new(ClaimTypes.UserData, JsonConvert.SerializeObject(user)),
                };
            if (user?.TenantId != null)
            {
                //租户ID
                claims.Add(new(ClaimTypes.PrimaryGroupSid, user.TenantId));
            }
            // 只挑选敏感权限
            var sensitivePerms = user.Permissions?.Where(p => p.StartsWith("p:")).ToList();
            if (sensitivePerms != null && sensitivePerms.Count > 0)
            {
                claims.Add(new Claim("sensitivePerms", string.Join(',', sensitivePerms)));
            }
            return claims;
        }

    }
}
