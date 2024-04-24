using System.ComponentModel.DataAnnotations;
using ZR.Common;

namespace ZR.Model.System.Dto
{
    public class ArticleQueryDto : PagerInfo
    {
        public long? UserId { get; set; }
        public string Status { get; set; }
        public string Title { get; set; }
        public string AbstractText { get; set; }
        public int? IsPublic { get; set; }
        public int? IsTop { get; set; }
        public int? CategoryId { get; set; }
        public DateTime? BeginTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? ArticleType { get; set; }
        /// <summary>
        /// 1、最新 2、私密 3、热门
        /// </summary>
        public int TabId { get; set; }
    }

    /// <summary>
    /// 输入输出对象
    /// </summary>
    public class ArticleDto
    {
        [Required(ErrorMessage = "Cid不能为空")]
        public long Cid { get; set; }
        [Required(ErrorMessage = "标题不能为空")]
        public string Title { get; set; }
        [Required(ErrorMessage = "内容不能为空")]
        public string Content { get; set; }

        public long? UserId { get; set; }

        public string Status { get; set; }

        public string FmtType { get; set; }

        public string Tags { get; set; }

        public int Hits { get; set; }

        public int? CategoryId { get; set; }

        public DateTime? CreateTime { get; set; }

        public DateTime? UpdateTime { get; set; }

        public string AuthorName { get; set; }

        public string CoverUrl { get; set; }

        public ArticleCategory ArticleCategoryNav { get; set; }
        public string[] TagList
        {
            get
            {
                return Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            }
        }
        public int IsPublic { get; set; } = 1;
        public string AbstractText { get; set; }
        public int IsTop { get; set; }
        /// <summary>
        /// 内容类型
        /// </summary>
        public int ArticleType { get; set; }
        /// <summary>
        /// 点赞数
        /// </summary>
        public int PraiseNum { get; set; }
        /// <summary>
        /// 评论数
        /// </summary>
        public int CommentNum { get; set; }
        /// <summary>
        /// 分享数
        /// </summary>
        public int ShareNum { get; set; }
        /// <summary>
        /// 用户IP
        /// </summary>
        [JsonIgnore]
        public string UserIP { get; set; }
        /// <summary>
        /// 地理位置
        /// </summary>
        [JsonIgnore]
        public string Location { get; set; }
        /// <summary>
        /// 手机型号
        /// </summary>
        public string PhoneModel { get; set; }
        /// <summary>
        /// 封面图片集合
        /// </summary>
        public string[] CoverImgList
        {
            get
            {
                return Tools.SplitAndConvert<string>(CoverUrl, ',');
            }
        }
        /// <summary>
        /// 地理位置
        /// </summary>
        public string Position
        {
            get
            {
                return Location?.Split("-")[0];
            }
        }
        public string Avatar { get; set; }
        public string NickName { get; set; }
        public int Sex { get; set; }
    }
}
