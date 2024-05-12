
namespace ZR.Model.Content
{
    /// <summary>
    /// 话题
    /// </summary>
    [SugarTable("article_topic", TableDescription = "文章话题")]
    [Tenant(0)]
    public class ArticleTopic
    {
        /// <summary>
        /// 话题ID 
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long TopicId { get; set; }

        /// <summary>
        /// 话题名 
        /// </summary>
        public string TopicName { get; set; }

        /// <summary>
        /// 话题描述 
        /// </summary>
        public string TopicDescription { get; set; }

        /// <summary>
        /// 参与/发起次数 
        /// </summary>
        public int JoinNum { get; set; }

        /// <summary>
        /// 浏览次数 
        /// </summary>
        public int ViewNum { get; set; }

        /// <summary>
        /// 创建时间 
        /// </summary>
        public DateTime? AddTime { get; set; }

        /// <summary>
        /// 话题分类 
        /// </summary>
        public int? TopicType { get; set; }

    }
}