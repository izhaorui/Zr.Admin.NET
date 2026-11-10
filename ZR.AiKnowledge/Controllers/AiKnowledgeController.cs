using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ZR.AiKnowledge.Model;

namespace ZR.AiKnowledge.Controllers
{
    /// <summary>
    /// 知识库连通性自查（管理端排查用）：用一次真实检索确认 Endpoint / ApiKey / 响应解析是否正确。
    /// <para>
    /// 定位：只读诊断接口。不写库、不进对话历史、不占对话额度，也不暴露 ApiKey。
    /// 权限复用 AI 治理查看权限，不新增权限码；插件关闭时直接返回“未启用”。
    /// </para>
    /// <para>控制器随 ZR.* 程序集由宿主自动装配（AddDynamicApi），插件工程内自带、核心零改动。</para>
    /// </summary>
    [Route("ai/knowledge")]
    [ApiExplorerSettings(GroupName = "ai")]
    public class AiKnowledgeController : BaseController
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>所有可用渠道（generic / dify / 未来自建），按配置 Provider 选择。</summary>
        private readonly IReadOnlyList<IKnowledgeProvider> _providers;

        public AiKnowledgeController(IEnumerable<IKnowledgeProvider> providers)
        {
            _providers = (providers ?? Enumerable.Empty<IKnowledgeProvider>()).ToList();
        }

        /// <summary>
        /// 用一次真实检索验证配置：返回渠道、耗时、命中数与片段摘要（片段截断，不含密钥）。
        /// </summary>
        [HttpPost("test")]
        [ActionPermissionFilter(Permission = "ai:governance:list")]
        public async Task<IActionResult> Test([FromBody] AiKnowledgeTestInput input)
        {
            var options = AiKnowledgeSettings.Current;
            if (!options.Enable)
            {
                return ToResponse(ResultCode.FAIL, "知识库功能未启用（AiKnowledge:Enable=false）");
            }

            var question = input?.Query?.Trim();
            if (string.IsNullOrWhiteSpace(question))
            {
                return ToResponse(ResultCode.FAIL, "请填写要检索的问题");
            }
            if (question.Length > 500)
            {
                return ToResponse(ResultCode.FAIL, "问题过长，请控制在 500 字以内");
            }

            var provider = _providers.FirstOrDefault(p =>
                string.Equals(p.Name, options.Provider, StringComparison.OrdinalIgnoreCase));
            if (provider == null)
            {
                Logger.Error($"知识库渠道未实现 provider={options.Provider} 已注册=[{string.Join(",", _providers.Select(p => p.Name))}]");
                return ToResponse(ResultCode.FAIL, "知识库配置有误（未找到对应的检索渠道）");
            }

            var query = new KnowledgeQuery
            {
                Question = question,
                TopK = Math.Clamp(input?.TopK ?? options.TopK, 1, 20),
                MinScore = options.MinScore,
                KbId = options.KbId
            };

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = await provider.SearchAsync(query);
                stopwatch.Stop();
                var hits = result?.Hits ?? new List<KnowledgeHit>();
                Logger.Info($"知识库连通性测试成功 provider={provider.Name} count={hits.Count} elapsedMs={stopwatch.ElapsedMilliseconds} q={AiHelper.ClipText(question, 100)}");

                return SUCCESS(new
                {
                    provider = provider.Name,
                    kbName = result?.KbName ?? options.KbName,
                    elapsedMs = stopwatch.ElapsedMilliseconds,
                    count = hits.Count,
                    hits = hits.Select(h => new
                    {
                        content = AiHelper.ClipText(h.Content, 300),
                        h.Score,
                        h.Title,
                        h.Source,
                        h.Locator
                    })
                });
            }
            catch (KnowledgeProviderException ex)
            {
                // 可预期失败：详情（含地址/上游报文）只进日志，返回可行动提示
                Logger.Warn($"知识库连通性测试失败 provider={provider.Name} err={ex.Message}");
                return ToResponse(ResultCode.FAIL, ex.UserMessage);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"知识库连通性测试异常 provider={provider.Name}");
                return ToResponse(ResultCode.FAIL, "知识库检索失败，请查看服务端日志");
            }
        }
    }
}
