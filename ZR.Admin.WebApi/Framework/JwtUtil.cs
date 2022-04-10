using Infrastructure;
using Infrastructure.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using ZR.Admin.WebApi.Extensions;
using ZR.Model.System;
using ZR.Service.System;

namespace ZR.Admin.WebApi.Framework
{
    /// <summary>
    /// 2020-11-20
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

            if (!string.IsNullOrEmpty(token))
            {
                return ValidateJwtToken(ParseToken(token));
            }
            return null;
        }

        /// <summary>
        /// 生成token
        /// </summary>
        /// <param name="claims"></param>
        /// <param name="jwtSettings"></param>
        /// <returns></returns>
        public static string GenerateJwtToken(List<Claim> claims, JwtSettings jwtSettings)
        {
            var authTime = DateTime.Now;
            var expiresAt = authTime.AddMinutes(jwtSettings.Expire);
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(jwtSettings.SecretKey);
            claims.Add(new Claim("Audience", jwtSettings.Audience));
            claims.Add(new Claim("Issuer", jwtSettings.Issuer));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = jwtSettings.Issuer,
                Audience = jwtSettings.Audience,
                IssuedAt = authTime,//token生成时间
                Expires = expiresAt,
                //NotBefore = authTime,
                TokenType = "Bearer",
                //对称秘钥，签名证书
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
        /// <summary>
        /// 验证Token
        /// </summary>
        /// <returns></returns>
        public static TokenValidationParameters ValidParameters()
        {
            JwtSettings jwtSettings = new();
            AppSettings.Bind("JwtSettings", jwtSettings);

            if (jwtSettings == null || jwtSettings.SecretKey.IsEmpty())
            {
                throw new Exception("JwtSettings获取失败");
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
        /// 从令牌中获取数据声明
        /// </summary>
        /// <param name="token">令牌</param>
        /// <returns></returns>
        public static IEnumerable<Claim> ParseToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validateParameter = ValidParameters();
            token = token.Replace("Bearer ", "");
            try
            {
                tokenHandler.ValidateToken(token, validateParameter, out SecurityToken validatedToken);

                var jwtToken = tokenHandler.ReadJwtToken(token);
                return jwtToken.Claims;
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
        /// <param name="jwtToken"></param>
        /// <returns></returns>
        public static LoginUser ValidateJwtToken(IEnumerable<Claim> jwtToken)
        {
            try
            {
                var userData = jwtToken.FirstOrDefault(x => x.Type == ClaimTypes.UserData).Value;
                var loginUser = JsonConvert.DeserializeObject<LoginUser>(userData);
                var permissions = CacheService.GetUserPerms(GlobalConstant.UserPermKEY + loginUser?.UserId);
                if (loginUser?.UserName == "admin")
                {
                    permissions = new List<string>() { GlobalConstant.AdminPerm };
                }
                if (permissions == null) return null;
                loginUser.Permissions = permissions;
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
            if (user?.Permissions.Count > 50)
            {
                user.Permissions = new List<string>();
            }
            var claims = new List<Claim>()
                {
                    new Claim(ClaimTypes.PrimarySid, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(ClaimTypes.UserData, JsonConvert.SerializeObject(user))
                };

            return claims;
        }

    }
}
