namespace ZR.Model.Dto
{
    public class UserDto
    {
        public long UserId { get; set; }
        public string UserName { get; set; }
        public string NickName { get; set; }
        public string Email { get; set; }
        public string Remark { get; set; }
        public string Avatar { get; set; }
        /// <summary>
        /// 用户性别（0男 1女 2未知）
        /// </summary>
        public int Sex { get; set; }

        /// <summary>
        /// 最后登录IP
        /// </summary>
        public string LoginIP { get; set; }
    }
}
