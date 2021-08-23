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

namespace ZR.Admin.WebApi.Framework
{
    /// <summary>
    /// 2020-11-20
    /// </summary>
    public class JwtUtil
    {
        public static readonly string KEY = "asdfghjklzxcvbnm";

        /// <summary>
        /// 获取用户身份信息
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        public static LoginUser GetLoginUser(HttpContext httpContext)
        {
            string token = HttpContextExtension.GetToken(httpContext);
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
        /// <returns></returns>
        public static string GenerateJwtToken(List<Claim> claims)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(KEY);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                //Issuer = "",
                //Audience = "",
                Expires = DateTime.Now.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// 从令牌中获取数据声明
        /// </summary>
        /// <param name="token">令牌</param>
        /// <returns></returns>
        public static IEnumerable<Claim> ParseToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(KEY);
            try
            {
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    // set clockskew to zero so tokens expire exactly at token expiration time (instead of 5 minutes later)
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                //{{"alg":"HS256","typ":"JWT"}.{"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/sid":"2","unique_name":"ry","nameid":"2","given_name":"若依","nbf":1606654010,"exp":1606740410,"iat":1606654010}}
                var jwtToken = (JwtSecurityToken)validatedToken;
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
        private static LoginUser ValidateJwtToken(IEnumerable<Claim> jwtToken)
        {
            try
            {
                var userId = jwtToken.FirstOrDefault(x => x.Type == "primarysid").Value;
                var userName = jwtToken.FirstOrDefault(x => x.Type == "unique_name").Value;
                var userData = jwtToken.FirstOrDefault(x => x.Type == ClaimTypes.UserData).Value;

                LoginUser loginUser = JsonConvert.DeserializeObject<LoginUser>(userData);
                return loginUser;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
    }
}
