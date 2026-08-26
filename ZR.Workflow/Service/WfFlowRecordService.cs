using ZR.Workflow.Helper;

namespace ZR.Workflow.Service
{
    /// <summary>
    /// 审批记录服务
    /// </summary>
    [AppService(ServiceType = typeof(IWfFlowRecordService))]
    public class WfFlowRecordService : BaseService<WfFlowRecord>, IWfFlowRecordService
    {
        private readonly IWfAiService _aiService;

        public WfFlowRecordService(IWfAiService aiService)
        {
            _aiService = aiService;
        }

        public PagedInfo<WfFlowRecordDto> GetList(WfFlowRecordQueryDto parm)
        {
            var query = BuildRecordQuery()
                .WhereIF(parm.InstanceId != null, (r, n, i, d) => r.InstanceId == parm.InstanceId)
                .WhereIF(!string.IsNullOrEmpty(parm.Title), (r, n, i, d) => i.Title.Contains(parm.Title))
                .OrderByDescending(r => r.RecordId)
                .Select((r, n, i, d) => new WfFlowRecordDto
                {
                    RecordId = r.RecordId,
                    TaskId = r.TaskId,
                    InstanceId = r.InstanceId,
                    NodeId = r.NodeId,
                    NodeName = n.NodeName,
                    Title = i.Title,
                    FlowName = SqlFunc.IsNull(i.FlowName, d.FlowName),
                    ApplyUser = i.ApplyUser,
                    ApplyNickName = i.ApplyNickName,
                    InstanceStatus = i.Status,
                    Operator = r.Operator,
                    OperatorNickName = r.OperatorNickName,
                    Action = r.Action,
                    IsRead = r.IsRead,
                    Opinion = r.Opinion,
                    Summary = r.Summary,
                    Create_time = r.Create_time
                });
            return query.ToPage(parm);
        }

        /// <summary>
        /// 抄送给我：筛选操作人为当前用户且标记为抄送的记录（按 userId 精确匹配）
        /// </summary>
        public PagedInfo<WfFlowRecordDto> GetCcList(WfFlowRecordQueryDto parm, long userId)
        {
            var query = BuildRecordQuery()
                .Where((r, n, i, d) => r.Action == (int)WfAction.Cc)
                .Where((r, n, i, d) => r.OperatorId == userId)
                .WhereIF(parm.InstanceId != null, (r, n, i, d) => r.InstanceId == parm.InstanceId)
                .WhereIF(!string.IsNullOrEmpty(parm.Title), (r, n, i, d) => i.Title.Contains(parm.Title))
                .OrderBy(r => r.RecordId, OrderByType.Desc)
                .Select((r, n, i, d) => new WfFlowRecordDto
                {
                    RecordId = r.RecordId,
                    InstanceId = r.InstanceId,
                    NodeId = r.NodeId,
                    NodeName = n.NodeName,
                    Title = i.Title,
                    FlowName = SqlFunc.IsNull(i.FlowName, d.FlowName),
                    ApplyUser = i.ApplyUser,
                    ApplyNickName = i.ApplyNickName,
                    InstanceStatus = i.Status,
                    Operator = r.Operator,
                    OperatorNickName = r.OperatorNickName,
                    IsRead = r.IsRead,
                    Opinion = r.Opinion,
                    Summary = r.Summary,
                    Create_time = r.Create_time
                });
            return query.ToPage(parm);
        }

        private ISugarQueryable<WfFlowRecord, WfFlowNode, WfFlowInstance, WfFlowDefinition> BuildRecordQuery()
        {
            return Queryable()
                .LeftJoin<WfFlowNode>((r, n) => r.NodeId == n.NodeId)
                .LeftJoin<WfFlowInstance>((r, n, i) => r.InstanceId == i.InstanceId)
                .LeftJoin<WfFlowDefinition>((r, n, i, d) => i.FlowId == d.FlowId);
        }

        /// <summary>
        /// 当前用户抄送未读数量（Action=Cc 且 IsRead=false）
        /// </summary>
        public int GetUnreadCount(long userId)
        {
            return Queryable()
                .Where(r => r.OperatorId == userId && r.Action == (int)WfAction.Cc && !r.IsRead)
                .Count();
        }

        /// <summary>
        /// 标记抄送已读（仅更新当前用户作为收件人的记录，防止越权标记他人）
        /// </summary>
        public void Read(List<long> ids, long userId)
        {
            if (ids == null || ids.Count == 0) return;
            Context.Updateable<WfFlowRecord>()
                .SetColumns(r => new WfFlowRecord { IsRead = true })
                .Where(r => ids.Contains(r.RecordId) && r.OperatorId == userId)
                .ExecuteCommand();
        }

        /// <summary>
        /// 手动生成/重生成单条审批记录的 AI 摘要（前端详情页可触发）。读取记录及其实例表单，调用
        /// <see cref="IWfAiService.SummarizeApprovalAsync"/> 生成并写回 <see cref="WfFlowRecord.Summary"/>，返回摘要文本。
        /// 适用于自动落痕失败 / 记录无摘要 / 想重新生成的场景。AI 未启用或调用失败时抛出友好异常。
        /// </summary>
        public async Task<string> RegenerateSummary(long recordId)
        {
            var record = await Context.Queryable<WfFlowRecord>()
                .LeftJoin<WfFlowNode>((r, n) => r.NodeId == n.NodeId)
                .Where(r => r.RecordId == recordId)
                .Select((r, n) => new { Record = r, NodeName = n.NodeName })
                .FirstAsync();
            if (record == null)
            {
                throw new CustomException("审批记录不存在");
            }

            var inst = await Context.Queryable<WfFlowInstance>()
                .Where(i => i.InstanceId == record.Record.InstanceId)
                .FirstAsync();
            var formItems = await Context.Queryable<WfFlowDefinition>()
                .Where(d => d.FlowId == inst.FlowId)
                .Select(d => d.FormItems)
                .FirstAsync();
            // 表单字段技术名翻译为中文label，避免 input_1 等暴露给 AI/用户
            var formText = WfFormTextHelper.TranslateToText(inst.FormContent, formItems) ?? inst.FormContent;

            var result = await _aiService.SummarizeApprovalAsync(string.Empty, record.NodeName, record.Record.Opinion, formText);
            var summary = result?.Summary;
            if (string.IsNullOrWhiteSpace(summary))
            {
                throw new CustomException("AI 未返回摘要内容，请稍后重试");
            }

            await Context.Updateable<WfFlowRecord>()
                .SetColumns(r => r.Summary == summary)
                .Where(r => r.RecordId == recordId)
                .ExecuteCommandAsync();

            return summary;
        }
    }
}
