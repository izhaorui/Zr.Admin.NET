using System;

namespace ZR.Model.System.Dto
{
    public class SysUserDto
    {
        public long UserId { get; set; }
        public string UserName { get; set; }
        public string NickName { get; set; }
        public string Email { get; set; }
        public string Remark { get; set; }
        public string Phonenumber { get; set; }
        /// <summary>
        /// 用户性别（0男 1女 2未知）
        /// </summary>
        public int Sex { get; set; }
        public string Password { get; set; }
    }

    public class SysUserQueryDto
    {
        public long? UserId { get; set; }
        public string UserName { get; set; }
        public string NickName { get; set; }
        public string Email { get; set; }
        public string Remark { get; set; }
        public string Phonenumber { get; set; }
        /// <summary>
        /// 用户性别（0男 1女 2未知）
        /// </summary>
        public int Sex { get; set; }
        public DateTime? BeginTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int Status { get; set; }
        public long DeptId { get; set; }
    }
}
