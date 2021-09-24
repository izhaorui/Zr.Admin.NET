using System;
using System.Collections.Generic;
using System.Text;
using ZR.Model.System;

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
        /// '用户性别（0男 1女 2未知）',
        /// </summary>
        public int Sex { get; set; }
    }
}
