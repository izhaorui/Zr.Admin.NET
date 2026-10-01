namespace ZR.Model.Member.Dto
{
    /// <summary>
    /// 会员管理查询对象（后台）
    /// </summary>
    public class MemberQueryDto : PagerInfo
    {
        /// <summary>
        /// 手机号（模糊匹配）
        /// </summary>
        public string Phone { get; set; }

        /// <summary>
        /// 昵称（模糊匹配）
        /// </summary>
        public string NickName { get; set; }

        /// <summary>
        /// 账号状态 0正常 1禁用（null=全部）
        /// </summary>
        public int? Status { get; set; }

        /// <summary>
        /// 注册时间起
        /// </summary>
        public DateTime? BeginRegisterTime { get; set; }

        /// <summary>
        /// 注册时间止
        /// </summary>
        public DateTime? EndRegisterTime { get; set; }
    }

    /// <summary>
    /// 会员启用/禁用请求（后台）
    /// </summary>
    public class MemberStatusDto
    {
        /// <summary>
        /// 会员Id
        /// </summary>
        public long MemberId { get; set; }

        /// <summary>
        /// 目标状态 0正常 1禁用
        /// </summary>
        public int Status { get; set; }
    }
}
