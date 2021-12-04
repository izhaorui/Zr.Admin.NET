using OfficeOpenXml.Attributes;
using SqlSugar;

namespace ZR.Model.System
{
    [SugarTable("sys_post")]
    [Tenant("0")]
    public class SysPost : SysBase
    {
        /// <summary>
        /// 岗位Id
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long PostId { get; set; }
        public string PostCode { get; set; }
        public string PostName { get; set; }
        [EpplusIgnore]
        public int PostSort { get; set; }
        [EpplusIgnore]
        public string Status { get; set; }
    }
}
