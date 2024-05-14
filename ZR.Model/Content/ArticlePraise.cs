namespace ZR.Model.Content
{
    [SugarTable("article_praise", "内容点赞记录")]
    [Tenant("0")]
    public class ArticlePraise
    {
        [SugarColumn(IsPrimaryKey = true)]
        public long PId { get; set; }
        public long UserId { get; set; }
        public long ArticleId { get; set; }
        public string UserIP { get; set; }
        public long ToUserId { get; set; }
        public int IsDelete { get; set; }
        [SugarColumn(InsertServerTime = true)]
        public DateTime AddTime { get; set; }
        public string Location { get; set; }
    }
}
