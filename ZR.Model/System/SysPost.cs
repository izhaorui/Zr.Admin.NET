namespace ZR.Model.System
{
    [SqlSugar.SugarTable("sys_post")]
    public class SysPost: SysBase
    {
        /// <summary>
        /// 岗位Id
        /// </summary>
        [SqlSugar.SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long PostId { get; set; }
        public string PostCode { get; set; }
        public string PostName { get; set; }
        public int PostSort { get; set; }
        public string Status { get; set; }
    }
}
