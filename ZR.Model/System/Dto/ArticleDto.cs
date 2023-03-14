using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ZR.Model.System.Dto
{
    public class ArticleQueryDto : PagerInfo
    {
        public string Status { get; set; }
        public string Title { get; set; }
        public DateTime BeginTime { get; set; }
        public DateTime EndTime { get; set; }

    }

    /// <summary>
    /// 输入输出对象
    /// </summary>
    public class ArticleDto
    {
        [Required(ErrorMessage = "Cid不能为空")]
        public int Cid { get; set; }

        public string Title { get; set; }

        public string Content { get; set; }

        public long? UserId { get; set; }

        public string Status { get; set; }

        public string FmtType { get; set; }

        public string Tags { get; set; }

        public int? Hits { get; set; }

        public int? CategoryId { get; set; }

        public DateTime? CreateTime { get; set; }

        public DateTime? UpdateTime { get; set; }

        public string AuthorName { get; set; }

        public string CoverUrl { get; set; }

        public ArticleCategory ArticleCategoryNav { get; set; }
        public string[] TagList { get; set; }

    }
}
