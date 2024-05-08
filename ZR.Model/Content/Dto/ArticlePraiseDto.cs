namespace ZR.Model.Content.Dto
{
    public class ArticlePraiseDto
    {
        public long UserId { get; set; }
        public long ArticleId { get; set; }
        public string UserIP { get; set; }
        public long ToUserId { get; set; }
        public int IsDelete { get; set; }
        public string Location { get; set; }
    }
}
