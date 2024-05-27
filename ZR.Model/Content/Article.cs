using ZR.Model.Enum;

namespace ZR.Model.Content
{
    /// <summary>
    /// 文章表
    /// </summary>
    [SugarTable("article", "内容管理")]
    [Tenant("0")]
    public class Article
    {
        /// <summary>
        /// 文章id
        /// </summary>
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long Cid { get; set; }
        /// <summary>
        /// 文章标题
        /// </summary>
        [SugarColumn(ColumnDescription = "文章标题", Length = 254)]
        public string Title { get; set; }
        /// <summary>
        /// 发布时间
        /// </summary>
        [SugarColumn(ColumnDescription = "发布时间")]
        public DateTime? CreateTime { get; set; }
        /// <summary>
        /// 修改时间
        /// </summary>
        [SugarColumn(IsOnlyIgnoreInsert = true, ColumnDescription = "更新时间")]
        public DateTime? UpdateTime { get; set; }
        /// <summary>
        /// 文章内容
        /// </summary>
        [SugarColumn(ColumnDescription = "文章内容", ColumnDataType = StaticConfig.CodeFirst_BigString)]
        public string Content { get; set; }
        /// <summary>
        /// 作者名
        /// </summary>
        [SugarColumn(ColumnDescription = "作者名", Length = 20, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string AuthorName { get; set; }
        /// <summary>
        /// 发布者用户id
        /// </summary>
        [SugarColumn(ColumnDescription = "发布者用户id", ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long UserId { get; set; }
        /// <summary>
        /// 文章状态 1、发布 2、草稿
        /// </summary>
        [SugarColumn(ColumnDescription = "文章状态 1、发布 2、草稿", Length = 20)]
        public string Status { get; set; }
        /// <summary>
        /// 编辑器类型 markdown,html
        /// </summary>
        [SugarColumn(ColumnDescription = "编辑器类型markdown,html", ColumnName = "editorType", Length = 20, IsNullable = true)]
        public string EditorType { get; set; }
        /// <summary>
        /// 文章标签eg：Net5,java
        /// </summary>
        [SugarColumn(ColumnDescription = "文章标签", Length = 20)]
        public string Tags { get; set; }
        /// <summary>
        /// 点击量
        /// </summary>
        [SugarColumn(ColumnDescription = "点击量", DefaultValue = "0")]
        public int Hits { get; set; }
        [SugarColumn(ColumnDescription = "目录id", ColumnName = "category_Id")]
        public int CategoryId { get; set; }
        /// <summary>
        /// 封面地址
        /// </summary>
        [SugarColumn(ColumnDescription = "封面地址", Length = 5000)]
        public string CoverUrl { get; set; }
        /// <summary>
        /// 是否公开 1、公开 0、不公开
        /// </summary>
        [SugarColumn(ColumnDescription = "是否公开 1、公开 0、不公开", DefaultValue = "0")]
        public int IsPublic { get; set; }
        /// <summary>
        /// 是否置顶
        /// </summary>
        public int IsTop { get; set; }
        /// <summary>
        /// 0、文章 1、随笔 2、动态
        /// </summary>
        [SugarColumn(ColumnDescription = "内容类型0、文章 1、随笔 2、动态", DefaultValue = "0")]
        public ArticleTypeEnum ArticleType { get; set; }
        /// <summary>
        /// 摘要
        /// </summary>
        public string AbstractText { get; set; }
        /// <summary>
        /// 分类
        /// </summary>

        [Navigate(NavigateType.OneToOne, nameof(CategoryId), nameof(ArticleCategory.CategoryId))] //自定义关系映射
        public ArticleCategory ArticleCategoryNav { get; set; }
        /// <summary>
        /// 评论数
        /// </summary>
        public int CommentNum { get; set; }
        /// <summary>
        /// 点赞数
        /// </summary>
        public int PraiseNum { get; set; }
        /// <summary>
        /// 用户IP
        /// </summary>
        public string UserIP { get; set; }
        /// <summary>
        /// 地理位置
        /// </summary>
        public string Location { get; set; }
        /// <summary>
        /// 话题ID
        /// </summary>
        public long TopicId { get; set; }
        /// <summary>
        /// 评论开关 0、所有人可评论 1、仅粉丝 2、仅自己
        /// </summary>
        public CommentSwitchEnum CommentSwitch { get; set; }
        /// <summary>
        /// 审核状态 0、待审核 1、通过 2、拒绝
        /// </summary>
        public AuditStatusEnum AuditStatus { get; set; }
    }
}
