namespace ZR.Model.System
{
    /// <summary>
    /// 通知公告表
    ///
    /// @author zr
    /// @date 2021-12-15
    /// </summary>
    [SugarTable("sys_notice", "通知公告表")]
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
        [SugarColumn(ColumnName = "notice_title", ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string NoticeTitle { get; set; }
        /// <summary>
        /// 公告类型 (1通知 2公告)
        /// </summary>
        [SugarColumn(ColumnName = "notice_type", ExtendedAttribute = ProteryConstant.NOTNULL)]
        public int NoticeType { get; set; }
        /// <summary>
        /// 公告内容
        /// </summary>
        [SugarColumn(ColumnName = "notice_content", ColumnDataType = StaticConfig.CodeFirst_BigString)]
        public string NoticeContent { get; set; }
        /// <summary>
        /// 公告状态 (0正常 1关闭)
        /// </summary>
        [SugarColumn(DefaultValue = "0", ExtendedAttribute = ProteryConstant.NOTNULL)]
        public int Status { get; set; }
        /// <summary>
        /// 发布人
        /// </summary>
        public string Publisher { get; set; }
        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime? BeginTime { get; set; }
        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime? EndTime { get; set; }
        /// <summary>
        /// 弹出提示
        /// </summary>
        public int Popup { get; set; }
    }
}