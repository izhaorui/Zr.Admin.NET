using Infrastructure.AI;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZR.Model.AI;
using ZR.Model.AI.Dto;
using ZR.ServiceCore.AI.IService;

namespace ZR.Admin.WebApi.Controllers.AI
{
    /// <summary>
    /// 全局 AI 助手（办工助手）：会话管理与工具调用对话。
    /// 权限按登录用户（common），会话/消息/工具数据均按当前登录用户隔离。
    /// </summary>
    [Route("aichat")]
    [ApiExplorerSettings(GroupName = "ai")]
    public class AiChatController : BaseController
    {
        private readonly ISysAiChatService _aiChatService;
        private readonly IAiCallGovernance _callGovernance;
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>图表导出单次行数上限，防止超大数据把内存与 Excel 生成拖垮</summary>
        private const int MaxChartExportRows = 5000;

        public AiChatController(ISysAiChatService aiChatService, IAiCallGovernance callGovernance)
        {
            _aiChatService = aiChatService;
            _callGovernance = callGovernance;
        }

        /// <summary>
        /// 当前登录用户在指定场景下的额度快照（默认 ai_chat）。只读，不占并发。
        /// </summary>
        [HttpGet("quota")]
        [ActionPermissionFilter(Permission = "common")]
        public async Task<IActionResult> Quota([FromQuery] string scene = AiSceneCatalog.AiChat)
        {
            return SUCCESS(await _callGovernance.GetMyQuotaAsync(scene));
        }

        /// <summary>
        /// 当前用户可见的工具展示名清单（Name + Label）。
        /// 前端据此渲染"正在调用 XX / 已调用 XX"，后端新增工具无需再改前端映射；
        /// 声明了权限的工具仅对有权限用户返回，未登记 Label 时前端降级显示工具名。
        /// </summary>
        [HttpGet("tools")]
        [ActionPermissionFilter(Permission = "common")]
        public async Task<IActionResult> Tools()
        {
            var userId = HttpContext.GetUId();
            return SUCCESS(await _aiChatService.GetMyToolCatalogAsync(userId));
        }

        /// <summary>
        /// 【管理】全量 AI 工具目录：列出内置工具与各模块 Provider 注册的扩展工具及其
        /// Provider/来源/权限/说明，<b>不做用户权限过滤</b>，仅供排查"功能对应哪个工具、落在哪个模块"。
        /// 复用 ai:governance:list 管理权限，不新增权限码。
        /// </summary>
        [HttpGet("tools/catalog")]
        [ActionPermissionFilter(Permission = "ai:governance:list")]
        public async Task<IActionResult> ToolCatalog()
        {
            return SUCCESS(await _aiChatService.GetToolCatalogAsync());
        }

        /// <summary>
        /// 会话列表（按当前用户，最近更新倒序，最多 50 条）
        /// </summary>
        [HttpGet("sessions")]
        [ActionPermissionFilter(Permission = "common")]
        public async Task<IActionResult> Sessions()
        {
            var userId = HttpContext.GetUId();
            return SUCCESS(await _aiChatService.ListSessionsAsync(userId));
        }

        /// <summary>
        /// 新建空会话
        /// </summary>
        [HttpPost("session")]
        [ActionPermissionFilter(Permission = "common")]
        public async Task<IActionResult> CreateSession()
        {
            var userId = HttpContext.GetUId();
            return SUCCESS(await _aiChatService.CreateSessionAsync(userId));
        }

        /// <summary>
        /// 重命名会话
        /// </summary>
        [HttpPut("session/{sessionId}/rename")]
        [ActionPermissionFilter(Permission = "common")]
        public async Task<IActionResult> RenameSession(long sessionId, [FromBody] SysAiChatRenameDto parm)
        {
            if (parm == null || string.IsNullOrWhiteSpace(parm.Title))
            {
                return ToResponse(ResultCode.FAIL, "标题不能为空");
            }
            var userId = HttpContext.GetUId();
            return SUCCESS(await _aiChatService.RenameSessionAsync(sessionId, userId, parm.Title.Trim()));
        }

        /// <summary>
        /// 删除会话（连同历史消息）
        /// </summary>
        [HttpDelete("session/{sessionId}")]
        [ActionPermissionFilter(Permission = "common")]
        public async Task<IActionResult> DeleteSession(long sessionId)
        {
            var userId = HttpContext.GetUId();
            return SUCCESS(await _aiChatService.DeleteSessionAsync(sessionId, userId));
        }

        /// <summary>
        /// 会话历史消息（升序）
        /// </summary>
        [HttpGet("session/{sessionId}/messages")]
        [ActionPermissionFilter(Permission = "common")]
        public async Task<IActionResult> Messages(long sessionId)
        {
            var userId = HttpContext.GetUId();
            return SUCCESS(await _aiChatService.GetMessagesAsync(sessionId, userId));
        }

        /// <summary>
        /// 发送对话消息（SessionId 传 0 自动新建会话；内部自动调用工具）
        /// </summary>
        [HttpPost("chat")]
        [ActionPermissionFilter(Permission = "common")]
        public async Task<IActionResult> Chat([FromBody] SysAiChatRequestDto parm)
        {
            if (!IsChatRequestValid(parm))
            {
                return ToResponse(ResultCode.FAIL, "请输入文字或上传图片");
            }
            var userId = HttpContext.GetUId();
            return SUCCESS(await _aiChatService.ChatAsync(parm.SessionId, userId, parm.Message, parm.ImageUrls));
        }

        /// <summary>
        /// 流式对话（SSE，text/event-stream）。入参与 Chat 一致，事件协议见 SysAiChatStreamDto：
        /// delta=增量文本（打字机效果）、tool=工具执行状态(start|done)、done=整轮结束(含落库结果与图表)、error=异常终止。
        /// </summary>
        [HttpPost("chat/stream")]
        [ActionPermissionFilter(Permission = "common")]
        public async Task ChatStream([FromBody] SysAiChatRequestDto parm)
        {
            if (!IsChatRequestValid(parm))
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                await Response.WriteAsync("{\"msg\":\"请输入文字或上传图片\"}", HttpContext.RequestAborted);
                return;
            }
            var userId = HttpContext.GetUId();
            var aborted = HttpContext.RequestAborted;
            // SSE 响应：关闭代理缓冲，保证逐块实时推送
            Response.ContentType = "text/event-stream; charset=utf-8";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Append("Connection", "keep-alive");
            Response.Headers["X-Accel-Buffering"] = "no";

            // 响应体写串行化：心跳任务与主事件流共用同一响应流，防交错
            using var gate = new SemaphoreSlim(1, 1);
            using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(aborted);

            // 写原始 SSE 文本（data 行 / 心跳注释行），带锁 + 逐次 flush
            async Task WriteRawAsync(string sseText, CancellationToken ct)
            {
                await gate.WaitAsync(ct);
                try
                {
                    await Response.WriteAsync(sseText, ct);
                    await Response.Body.FlushAsync(ct);
                }
                finally
                {
                    gate.Release();
                }
            }

            // 序列化 DTO 为 data 事件写出
            async Task WriteEventAsync(SysAiChatStreamDto evt, CancellationToken ct)
            {
                var json = JsonSerializer.Serialize(evt, SseJsonOptions);
                await WriteRawAsync($"data: {json}\n\n", ct);
            }

            // 20s 心跳：Nginx proxy_read_timeout 默认按"两次读取间空闲时长"掐连接，
            // 工具执行/模型换轮等停顿段没有数据，需心跳保活整条链路（独立于主事件循环）。
            var pingTask = Task.Run(async () =>
            {
                try
                {
                    while (!pingCts.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(20), pingCts.Token);
                        await WriteRawAsync(": ping\n\n", pingCts.Token);
                    }
                }
                catch (OperationCanceledException) { } // 正常结束/客户端断开
                catch (IOException) { }                // 连接中断
                catch (Exception ex)
                {
                    _logger.Error($"AI SSE 心跳写入失败：{ex.Message}");
                }
            }, aborted);

            try
            {
                await foreach (var evt in _aiChatService.StreamChatAsync(parm.SessionId, userId, parm.Message, parm.ImageUrls, aborted))
                {
                    await WriteEventAsync(evt, aborted);
                }
                // done 事件由 StreamChatAsync 末尾 yield 的 done chunk 统一发出，无需在此补发
            }
            catch (OperationCanceledException) { } // 客户端断开/请求取消：静默收尾
            catch (IOException) { }                // 连接中断：静默收尾（不写 error 事件）
            catch (Exception ex)
            {
                _logger.Error($"AI 流式对话异常 sessionId={parm.SessionId} userId={userId} msg={AiHelper.ClipText(parm.Message, 200)} err={ex}");
                try
                {
                    await WriteEventAsync(new SysAiChatStreamDto { Type = "error", Error = ex.Message }, aborted);
                }
                catch
                {
                    // 写入失败（客户端已断开）则忽略
                }
            }
            finally
            {
                // 停心跳并等它退出，避免 using 释放 gate/pingCts 时仍有写入
                pingCts.Cancel();
                try { await pingTask; } catch { /* pingTask 内部已兜底，正常不会抛出 */ }
            }
        }

        /// <summary>
        /// 导出助手图表结果为 Excel。
        ///
        /// 数据来自本轮对话 <c>done.Charts</c>（后端已聚合、已按数据集权限校验），前端展开后回传，
        /// 服务端只负责落 Excel，不重新查数 —— 避免前端凭参数绕过数据集权限。
        /// </summary>
        [HttpPost("chart/export")]
        [ActionPermissionFilter(Permission = "common")]
        [Log(Title = "AI图表导出", BusinessType = BusinessType.EXPORT, IsSaveRequestData = false, IsSaveResponseData = false)]
        public IActionResult ExportChart([FromBody] AiChartExportInput parm)
        {
            if (parm?.Rows == null || parm.Rows.Count == 0)
            {
                return ToResponse(ResultCode.FAIL, "没有要导出的图表数据");
            }
            if (parm.Rows.Count > MaxChartExportRows)
            {
                return ToResponse(ResultCode.FAIL, $"单次最多导出 {MaxChartExportRows} 行图表数据");
            }

            var columns = (parm.Columns ?? new List<AiChartExportColumn>())
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Field))
                .Select(c => (c.Field, c.Label))
                .ToList();

            var title = string.IsNullOrWhiteSpace(parm.Title) ? "图表数据" : parm.Title.Trim();
            var result = ExportChartExcel(parm.Rows, columns, title, title);
            return ExportExcel(result.Item2, result.Item1);
        }

        private static bool IsChatRequestValid(SysAiChatRequestDto parm)
        {
            if (parm == null) return false;
            if (!string.IsNullOrWhiteSpace(parm.Message)) return true;
            return parm.ImageUrls != null && parm.ImageUrls.Any(u => !string.IsNullOrWhiteSpace(u));
        }

        private static readonly JsonSerializerOptions SseJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}
