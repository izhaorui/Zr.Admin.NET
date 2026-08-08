namespace ZR.Model.System.Dto
{
    public class SysDeptQueryDto : PagerInfo
    {
        public int? Status { get; set; }
        public int? DelFlag { get; set; }
        public string DeptName { get; set; }
    }
    public class SysDeptDto : SysBase
    {
        public long DeptId { get; set; }

        public long ParentId { get; set; }

        public string Ancestors { get; set; }

        public string DeptName { get; set; }

        public int OrderNum { get; set; }

        public string Leader { get; set; }

        /// <summary>
        /// 负责人用户Id集合，多个以逗号分隔
        /// </summary>
        public string LeaderIds { get; set; }

        public string Phone { get; set; }

        public string Email { get; set; }

        public int Status { get; set; }

        public int DelFlag { get; set; }
        public int UserNum { get; set; }
    }
}
