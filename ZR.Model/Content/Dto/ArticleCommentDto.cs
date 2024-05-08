namespace ZR.Model.Content.Dto
{
    /// <summary>
    /// 评论
    /// </summary>
    public class MessageQueryDto : PagerInfo
    {
        public long CommentId { get; set; }
        public long ParentId { get; set; }
        public int OrderBy { get; set; }
        public long? UserId { get; set; }
        public int ClassifyId { get; set; }
        public long TargetId { get; set; }
        public DateTime? BeginAddTime { get; set; }
        public DateTime? EndAddTime { get; set; }
    }

    public class ArticleCommentDto
    {
        [JsonConverter(typeof(ValueToStringConverter))]
        public long CommentId { get; set; }
        /// <summary>
        /// 用户id
        /// </summary>
        public long UserId { get; set; }
        /// <summary>
        /// 留言内容
        /// </summary>
        public string Content { get; set; }
        /// <summary>
        /// 最顶级留言id
        /// </summary>
        [JsonConverter(typeof(ValueToStringConverter))]
        public long ParentId { get; set; }
        /// <summary>
        /// 评论时间
        /// </summary>
        public DateTime AddTime { get; set; }
        /// <summary>
        /// 回复用户id
        /// </summary>
        public int ReplyUserId { get; set; }
        public string ReplyNickName { get; set; }
        /// <summary>
        /// 回复留言id
        /// </summary>
        [JsonConverter(typeof(ValueToStringConverter))]
        public long ReplyId { get; set; }
        /// <summary>
        /// 用户ip
        /// </summary>
        [JsonIgnore]
        public string UserIP { get; set; }
        /// <summary>
        /// 地理位置
        /// </summary>
        [JsonIgnore]
        public string Location { get; set; } = string.Empty;
        /// <summary>
        /// 喜欢次数
        /// </summary>
        public int PraiseNum { get; set; }

        /// <summary>
        /// 评论次数
        /// </summary>
        public int ReplyNum { get; set; }
        ///// <summary>
        ///// 审核状态 0、待审核 1、通过 -1、未通过
        ///// </summary>
        //public int AuditStatus { get; set; }
        /// <summary>
        /// 描述 :是否删除 1、删除 0、正常
        /// 空值 : true  
        /// </summary>
        [JsonIgnore]
        public int IsDelete { get; set; } = 0;
        public string NickName { get; set; }

        ///// <summary>
        ///// 分享次数
        ///// </summary>
        //public int ShareNunm { get; set; }
        public string Avatar { get; set; } = string.Empty;
        public string ChatImg { get; set; }
        /// <summary>
        /// 分类id
        /// </summary>
        public int ClassifyId { get; set; }
        /// <summary>
        /// 目标id
        /// </summary>
        public long TargetId { get; set; }
        /// <summary>
        /// 置顶
        /// </summary>
        public long Top { get; set; }
        public bool HasMore { get; set; } = false;
        public string Position
        {
            get
            {
                var temp_location = Location.Split("-")?[0];

                if (temp_location == "0")
                {
                    return "IP未知";
                }
                return temp_location?.Replace("省", "");
            }
        }
        /// <summary>
        /// 回复列表
        /// </summary>
        public List<ArticleCommentDto> ReplyList { get; set; }
    }
}
