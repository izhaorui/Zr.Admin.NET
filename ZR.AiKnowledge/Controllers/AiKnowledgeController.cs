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

        private readonly IAiKnowledgeQueryService _queryService;

        /// <summary>权限服务：只用于把"当前用户能不能用某个库"算给前端，不改变工具侧判断。</summary>
        private readonly ISysPermissionService _permissionService;

        public AiKnowledgeController(IEnumerable<IKnowledgeProvider> providers, IAiKnowledgeQueryService queryService,
            ISysPermissionService permissionService = null)
        {
            _providers = (providers ?? Enumerable.Empty<IKnowledgeProvider>()).ToList();
            _queryService = queryService;
            _permissionService = permissionService;
        }

        /// <summary>
        /// 已生效的知识库清单：管理端自查与前端选择器共用。
        /// <para>缺地址/缺数据集的库带 <c>configured=false</c>，"配了但一直报错"能一眼定位。</para>
        /// </summary>
        [HttpGet("base/list")]
        [ActionPermissionFilter(Permission = "ai:governance:list")]
        public IActionResult BaseList()
        {
            var options = AiKnowledgeSettings.Current;
            var bases = options.BuildBases();
            var perms = _permissionService == null
                ? null
                : AiPermissionHelper.TryLoadPerms(_permissionService, HttpContext.GetUId());

            return SUCCESS(bases.Select(b => new AiKnowledgeBaseDto
            {
                Id = b.Id,
                Name = b.Name,
                Description = b.Description,
                Provider = b.Provider,
                IsDefault = b.IsDefault,
                TopK = b.TopK ?? options.TopK,
                MinScore = b.MinScore ?? options.MinScore,
                KbId = b.KbId,
                Permission = b.Permission,
                CanUse = AiKnowledgeBaseResolver.CanUse(b, perms),
                Configured = AiKnowledgeBaseResolver.IsConfigured(b)
            }).ToList());
        }

        /// <summary>
        /// 检索审计列表（按时间倒序）：问题、命中数、耗时、错误分类与命中文档摘要。
        /// <para>权限复用 AI 治理查看权限，不新增权限码。</para>
        /// </summary>
        [HttpGet("query/list")]
        [ActionPermissionFilter(Permission = "ai:governance:list")]
        public IActionResult QueryList([FromQuery] AiKnowledgeQueryDto query) => SUCCESS(_queryService.GetList(query));

        /// <summary>
        /// 检索概览统计（默认近 7 天，最大 90 天）：命中率、空结果、失败数、平均/最大耗时、
        /// 按天趋势与 Top 10 未命中问题（知识库覆盖缺口的直接依据）。
        /// </summary>
        [HttpGet("query/stat")]
        [ActionPermissionFilter(Permission = "ai:governance:list")]
        public IActionResult QueryStat([FromQuery] int days = 7) => SUCCESS(_queryService.GetStat(days));

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

            var bases = options.BuildBases();
            var selected = AiKnowledgeBaseResolver.Pick(bases, input?.KbId);
            if (bases.Count == 0)
            {
                return ToResponse(ResultCode.FAIL, "知识库未配置或已全部停用");
            }
            if (selected == null)
            {
                return ToResponse(ResultCode.FAIL,
                    $"未找到知识库「{input?.KbId}」，可用：{string.Join("、", bases.Select(b => b.Id))}");
            }

            var provider = _providers.FirstOrDefault(p =>
                string.Equals(p.Name, selected.Provider, StringComparison.OrdinalIgnoreCase));
            if (provider == null)
            {
                Logger.Error($"知识库渠道未实现 provider={selected.Provider} base={selected.Id} 已注册=[{string.Join(",", _providers.Select(p => p.Name))}]");
                return ToResponse(ResultCode.FAIL, "知识库配置有误（未找到对应的检索渠道）");
            }

            var query = new KnowledgeQuery
            {
                Question = question,
                TopK = Math.Clamp(input?.TopK ?? selected.TopK ?? options.TopK, 1, 20),
                MinScore = selected.MinScore ?? options.MinScore,
                KbId = selected.KbId
            };
            var connection = AiKnowledgeBaseResolver.ToConnection(selected);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = await provider.SearchAsync(query, connection);
                stopwatch.Stop();
                var hits = result?.Hits ?? new List<KnowledgeHit>();
                Logger.Info($"知识库连通性测试成功 base={selected.Id} provider={provider.Name} count={hits.Count} elapsedMs={stopwatch.ElapsedMilliseconds} q={AiHelper.ClipText(question, 100)}");

                return SUCCESS(new
                {
                    provider = provider.Name,
                    kbId = selected.Id,
                    kbName = result?.KbName ?? selected.Name,
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
                Logger.Warn($"知识库连通性测试失败 base={selected.Id} provider={provider.Name} err={ex.Message}");
                return ToResponse(ResultCode.FAIL, ex.UserMessage);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"知识库连通性测试异常 base={selected.Id} provider={provider.Name}");
                return ToResponse(ResultCode.FAIL, "知识库检索失败，请查看服务端日志");
            }
        }
    }
}
