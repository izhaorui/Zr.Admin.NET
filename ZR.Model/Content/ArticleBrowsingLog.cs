namespace ZR.Model.Content
{
    [SugarTable("article_browsing_log", "内容浏览记录")]
    [Tenant("0")]
    public class ArticleBrowsingLog
    {
        [SugarColumn(IsPrimaryKey = true)]
        public long LogId { get; set; }
        public string Location { get; set; }
        public string UserIP { get; set; }
        public DateTime AddTime { get; set; }
        /// <summary>
        /// 文章ID
        /// </summary>
        public long ArticleId { get; set; }
        public long UserId { get; set; }
    }
}
