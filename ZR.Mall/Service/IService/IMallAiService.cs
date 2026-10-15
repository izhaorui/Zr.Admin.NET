using ZR.Mall.Model.Dto;

namespace ZR.Mall.Service.IService
{
    /// <summary>
    /// 商城 AI 服务：商品营销文案生成（标题/卖点/详情描述/SEO 关键词）。
    /// 与系统 AI、工作流 AI 共用同一套基建（AiHelper / PromptLoader / AiSceneCatalog），
    /// 提示词放在 Prompts/mall/ 下，可热改无需重编译。
    /// </summary>
    public interface IMallAiService
    {
        /// <summary>
        /// 生成商品营销文案草稿（不落库）。未配置 AI 或解析失败时抛异常，由 Controller 捕获返回友好提示。
        /// </summary>
        Task<MallProductCopyResult> GenerateProductCopyAsync(MallProductCopyInput input);
    }
}
