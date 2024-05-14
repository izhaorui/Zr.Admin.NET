using System.ComponentModel.DataAnnotations;

namespace ZR.Model.System.Dto
{
    public class RegisterDto
    {
        /// <summary>
        /// 用户名
        /// </summary>
        [Required(ErrorMessage = "用户名不能为空")]
        public string Username { get; set; }

        /// <summary>
        /// 用户密码
        /// </summary>
        [Required(ErrorMessage = "密码不能为空")]
        public string Password { get; set; }
        [Required(ErrorMessage = "确认密码不能为空")]
        public string ConfirmPassword { get; set; }
        /// <summary>
        /// 验证码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 唯一标识
        /// </summary>
        public string Uuid { get; set; } = "";
        /// <summary>
        /// 头像
        /// </summary>
        public string Photo { get; set; }
        public string UserIP { get; set; }
    }
}
