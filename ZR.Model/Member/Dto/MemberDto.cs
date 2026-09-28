using System.ComponentModel.DataAnnotations;

namespace ZR.Model.Member.Dto
{
    /// <summary>
    /// 发送会员登录验证码请求
    /// </summary>
    public class MemberSendCodeDto
    {
        /// <summary>
        /// 会员手机号
        /// </summary>
        [Required(ErrorMessage = "手机号不能为空")]
        public string Phone { get; set; }
    }

    /// <summary>
    /// 会员登录请求（未注册手机号自动注册，无需单独注册接口）
    /// </summary>
    public class MemberLoginDto
    {
        /// <summary>
        /// 会员手机号
        /// </summary>
        [Required(ErrorMessage = "手机号不能为空")]
        public string Phone { get; set; }

        /// <summary>
        /// 短信验证码
        /// </summary>
        [Required(ErrorMessage = "验证码不能为空")]
        public string Code { get; set; }
    }

    /// <summary>
    /// 会员资料更新请求（仅更新非空字段）
    /// </summary>
    public class MemberUpdateDto
    {
        /// <summary>
        /// 昵称（可选）
        /// </summary>
        public string NickName { get; set; }

        /// <summary>
        /// 头像地址（可选）
        /// </summary>
        public string Avatar { get; set; }
    }

    /// <summary>
    /// 会员信息输出（不含敏感字段）
    /// </summary>
    public class MemberInfoDto
    {
        /// <summary>
        /// 会员Id
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 手机号
        /// </summary>
        public string Phone { get; set; }

        /// <summary>
        /// 昵称
        /// </summary>
        public string NickName { get; set; }

        /// <summary>
        /// 头像地址
        /// </summary>
        public string Avatar { get; set; }

        /// <summary>
        /// 性别 0未知 1男 2女
        /// </summary>
        public int Gender { get; set; }

        /// <summary>
        /// 账号状态 0正常 1禁用
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// 会员等级
        /// </summary>
        public int MemberLevel { get; set; }

        /// <summary>
        /// 注册时间
        /// </summary>
        public DateTime RegisterTime { get; set; }

        /// <summary>
        /// 最近登录时间
        /// </summary>
        public DateTime? LastLoginTime { get; set; }
    }
}
