
namespace ZR.Model.Public
{
    /// <summary>
    /// 广告管理
    /// </summary>
    [SugarTable("banner_config")]
    public class BannerConfig
    {
        /// <summary>
        /// id 
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>
        /// Title 
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 说明 
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 链接 
        /// </summary>
        public string Link { get; set; }

        /// <summary>
        /// 图片 
        /// </summary>
        public string ImgUrl { get; set; }

        /// <summary>
        /// 跳转类型 0、不跳转 1、外链 2、内部跳转
        /// </summary>
        public int JumpType { get; set; }

        /// <summary>
        /// 添加时间 
        /// </summary>
        public DateTime? AddTime { get; set; }

        /// <summary>
        /// 点击次数 
        /// </summary>
        public int? ClicksNumber { get; set; }

        /// <summary>
        /// 是否显示 
        /// </summary>
        public int ShowStatus { get; set; }

        /// <summary>
        /// 广告类型 
        /// </summary>
        public int AdType { get; set; }

        /// <summary>
        /// BeginTime 
        /// </summary>
        public DateTime? BeginTime { get; set; }

        /// <summary>
        /// EndTime 
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// 排序id 
        /// </summary>
        public int? SortId { get; set; }

    }
}