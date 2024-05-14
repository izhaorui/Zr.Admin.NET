namespace ZR.Model.Content
{
    [SugarTable("article_comment", "评论表")]
    [Tenant(0)]
    public class ArticleComment
    {
        /// <summary>
        /// 评论ID
        /// </summary>
        [SugarColumn(IsPrimaryKey = true)]
        [JsonConverter(typeof(ValueToStringConverter))]
        public long CommentId { get; set; }
        /// <summary>
        /// 用户id
        /// </summary>
        public long UserId { get; set; }
        /// <summary>
        /// 内容
        /// </summary>
        public string Content { get; set; }
        /// <summary>
        /// 最顶级留言id
        /// </summary>
        public long ParentId { get; set; } = 0;
        /// <summary>
        /// 评论时间
        /// </summary>
        public DateTime AddTime { get; set; }
        /// <summary>
        /// 回复用户id
        /// </summary>
        public long ReplyUserId { get; set; }
        /// <summary>
        /// 回复留言id
        /// </summary>
        public long ReplyId { get; set; }
        /// <summary>
        /// 用户ip
        /// </summary>
        public string UserIP { get; set; }
        /// <summary>
        /// 地理位置
        /// </summary>
        public string Location { get; set; } = string.Empty;
        /// <summary>
        /// 喜欢次数
        /// </summary>
        public int PraiseNum { get; set; } = 0;

        /// <summary>
        /// 评论次数
        /// </summary>
        public int ReplyNum { get; set; } = 0;
        /// <summary>
        /// 审核状态 0、待审核 1、通过 -1、未通过
        /// </summary>
        public int AuditStatus { get; set; }
        /// <summary>
        /// 描述 :是否删除 1、删除 0、正常
        /// 空值 : true  
        /// </summary>
        [JsonIgnore]
        public int IsDelete { get; set; } = 0;
        /// <summary>
        /// 聊天图片
        /// </summary>
        public string ChatImg { get; set; }
        /// <summary>
        /// 分类id（可以对应表)
        /// </summary>
        public int ClassifyId { get; set; }
        /// <summary>
        /// 目标id(内容id)
        /// </summary>
        public long TargetId { get; set; }
        /// <summary>
        /// 是否置顶
        /// </summary>
        public long Top { get; set; }
        /// <summary>
        /// 作者回复过
        /// </summary>
        public int AuthorReply { get; set; }
    }
}
