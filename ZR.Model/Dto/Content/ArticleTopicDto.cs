namespace ZR.Model.Dto
{
    /// <summary>
    /// 文章话题查询对象
    /// </summary>
    public class ArticleTopicQueryDto : PagerInfo 
    {
    }

    /// <summary>
    /// 文章话题输入输出对象
    /// </summary>
    public class ArticleTopicDto
    {
        [Required(ErrorMessage = "话题ID不能为空")]
        [ExcelColumn(Name = "话题ID")]
        [ExcelColumnName("话题ID")]
        public long TopicId { get; set; }

        [Required(ErrorMessage = "话题名不能为空")]
        [ExcelColumn(Name = "话题名")]
        [ExcelColumnName("话题名")]
        public string TopicName { get; set; }

        [ExcelColumn(Name = "话题描述")]
        [ExcelColumnName("话题描述")]
        public string TopicDescription { get; set; } = string.Empty;

        [ExcelColumn(Name = "参与次数")]
        [ExcelColumnName("参与次数")]
        public int JoinNum { get; set; }

        [ExcelColumn(Name = "创建时间", Format = "yyyy-MM-dd HH:mm:ss", Width = 20)]
        [ExcelColumnName("创建时间")]
        public DateTime? AddTime { get; set; }
        /// <summary>
        /// 浏览次数
        /// </summary>
        [ExcelColumn(Name = "浏览次数")]
        [ExcelColumnName("浏览次数")]
        public int ViewNum { get; set; }
    }
}