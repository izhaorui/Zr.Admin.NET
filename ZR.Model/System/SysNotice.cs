using SqlSugar;

namespace ZR.Model.System
{
    /// <summary>
    /// 通知公告表，数据实体对象
    ///
    /// @author zr
    /// @date 2021-12-15
    /// </summary>
    [SugarTable("sys_notice")]
    [Tenant(0)]
    public class SysNotice : SysBase
    {
        /// <summary>
        /// 公告ID
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "notice_id")]
        public int NoticeId { get; set; }
        /// <summary>
        /// 公告标题
        /// </summary>
        [SugarColumn(ColumnName = "notice_title")]
        public string NoticeTitle { get; set; }
        /// <summary>
        /// 公告类型 (1通知 2公告)
        /// </summary>
        [SugarColumn(ColumnName = "notice_type")]
        public string NoticeType { get; set; }
        /// <summary>
        /// 公告内容
        /// </summary>
        [SugarColumn(ColumnName = "notice_content")]
        public string NoticeContent { get; set; }
        /// <summary>
        /// 公告状态 (0正常 1关闭)
        /// </summary>
        public string Status { get; set; }
    }
}