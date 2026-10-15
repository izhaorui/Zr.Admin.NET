namespace ZR.Mall.Model.Dto
{
    /// <summary>
    /// 商品营销文案生成入参。
    /// 以 <see cref="ProductId"/> 从库内商品补充素材（名称/分类/品牌/规格/价格），
    /// 其余为可选的口语化补充说明，帮助模型产出更贴合定位的文案。
    /// </summary>
    public class MallProductCopyInput
    {
        /// <summary>商品ID：必填。服务端据此读取商品信息作为生成素材，不信任前端传入的商品文案。</summary>
        public long ProductId { get; set; }

        /// <summary>关键词/卖点提示（可空）：用户希望文案包含的卖点、材质、场景等，逗号分隔</summary>
        public string Keywords { get; set; }

        /// <summary>语气风格（可空）：如 专业/亲切/高端/活泼，缺省由模型按商品类型判断</summary>
        public string Tone { get; set; }

        /// <summary>目标人群（可空）：如 宝妈/学生党/商务人士</summary>
        public string TargetAudience { get; set; }

        /// <summary>输出语言（可空）：默认中文；仅影响文案语种，不改变商品信息</summary>
        public string Language { get; set; }
    }

    /// <summary>
    /// 商品营销文案生成结果（草稿，不落库）：
    /// 标题 / 卖点 / 详情描述 / SEO 关键词。用户可复制后自行填入商品表单。
    /// </summary>
    public class MallProductCopyResult
    {
        /// <summary>商品标题（一句话主标题）</summary>
        public string Title { get; set; }

        /// <summary>核心卖点列表（3~5 条，每条一句）</summary>
        public List<string> SellingPoints { get; set; } = new();

        /// <summary>详情描述（Markdown/富文本友好的分段文字，不含脚本）</summary>
        public string Description { get; set; }

        /// <summary>SEO 关键词列表（用于商品搜索与站外推广）</summary>
        public List<string> SeoKeywords { get; set; } = new();

        /// <summary>模型未返回结构化 JSON 时的原文兜底（供前端提示重试）</summary>
        public string Raw { get; set; }
    }
}
