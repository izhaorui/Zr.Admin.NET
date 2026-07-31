using ZR.Workflow.Enum;

namespace ZR.Workflow.Service
{
    /// <summary>
    /// 流程实例服务
    /// </summary>
    [AppService(ServiceType = typeof(IWfFlowInstanceService))]
    public class WfFlowInstanceService : BaseService<WfFlowInstance>, IWfFlowInstanceService
    {
        private readonly IWfEngineService _engine;

        public WfFlowInstanceService(IWfEngineService engine)
        {
            _engine = engine;
        }

        /// <summary>
        /// 查询我的待办/已办/我发起/抄送的流程实例列表，按申请时间倒序分页返回。
        /// </summary>
        /// <param name="parm"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public PagedInfo<WfFlowInstanceDto> GetMyList(WfFlowInstanceQueryDto parm, long userId)
        {
            var predicate = Expressionable.Create<WfFlowInstance>();
            predicate = predicate.And(t => t.ApplyUserId == userId);
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Title), t => t.Title.Contains(parm.Title));
            predicate = predicate.AndIF(parm.Status != null, t => t.Status == parm.Status);
            predicate = predicate.AndIF(parm.FlowId != null, t => t.FlowId == parm.FlowId);
            var paged = Queryable().Where(predicate.ToExpression())
                .ToPage<WfFlowInstance, WfFlowInstanceDto>(parm);
            // 冗余 FlowName 可能为空，按 FlowId 关联流程定义兜底填充
            FillFlowName(paged.Result);
            // 填充当前节点名称
            FillCurrentNode(paged.Result);
            // 填充审批人（列表展示）
            FillApprovers(paged.Result);
            return paged;
        }

        private void FillFlowName(List<WfFlowInstanceDto> list)
        {
            if (list == null || list.Count == 0) return;
            var needIds = list.Where(x => string.IsNullOrEmpty(x.FlowName)).Select(x => x.FlowId).Distinct().ToList();
            if (needIds.Count == 0) return;
            var defs = Context.Queryable<WfFlowDefinition>()
                .Where(d => needIds.Contains(d.FlowId))
                .ToList();
            var map = defs.ToDictionary(d => d.FlowId, d => d.FlowName);
            foreach (var it in list)
            {
                if (string.IsNullOrEmpty(it.FlowName) && map.TryGetValue(it.FlowId, out var fn))
                    it.FlowName = fn;
            }
        }

        /// <summary>
        /// 按 CurrentNodeId 关联 wf_flow_node 填充当前节点名称（已结束/无当前节点则不填）。
        /// </summary>
        private void FillCurrentNode(List<WfFlowInstanceDto> list)
        {
            if (list == null || list.Count == 0) return;
            var needIds = list.Where(x => x.CurrentNodeId.HasValue && string.IsNullOrEmpty(x.CurrentNodeName))
                .Select(x => x.CurrentNodeId.Value).Distinct().ToList();
            if (needIds.Count == 0) return;
            var nodes = Context.Queryable<WfFlowNode>()
                .Where(n => needIds.Contains(n.NodeId))
                .ToList();
            var map = nodes.ToDictionary(n => n.NodeId, n => n.NodeName);
            foreach (var it in list)
            {
                if (it.CurrentNodeId.HasValue && string.IsNullOrEmpty(it.CurrentNodeName)
                    && map.TryGetValue(it.CurrentNodeId.Value, out var nn))
                    it.CurrentNodeName = nn;
            }
        }

        /// <summary>
        /// 填充审批人：进行中实例取当前待审任务审批人；已结束/撤回实例取全部参与审批人。
        /// 直接读取任务表的昵称快照，免运行时关联用户表。
        /// </summary>
        private void FillApprovers(List<WfFlowInstanceDto> list)
        {
            if (list == null || list.Count == 0) return;
            var ids = list.Select(x => x.InstanceId).Distinct().ToList();
            var tasks = Context.Queryable<WfFlowTask>().Where(t => ids.Contains(t.InstanceId)).ToList();
            foreach (var it in list)
            {
                var grp = tasks.Where(t => t.InstanceId == it.InstanceId);
                // 进行中：当前待审审批人；结束/撤回：所有参与审批人
                var relevant = it.Status == (int)WfInstanceStatus.Approval
                    ? grp.Where(t => t.Status == (int)WfTaskStatus.Pending)
                    : grp;
                var approvers = relevant
                    .Select(t => t.AssigneeNickName)
                    .Where(a => !string.IsNullOrEmpty(a))
                    .SelectMany(a => a.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    .Select(a => a.Trim())
                    .Distinct()
                    .ToList();
                it.Approvers = string.Join(",", approvers);
            }
        }

        public WfFlowInstanceDto GetInfo(long instanceId)
        {
            var inst = Queryable().First(i => i.InstanceId == instanceId);
            if (inst == null) return null;
            var dto = inst.Adapt<WfFlowInstanceDto>();
            if (string.IsNullOrEmpty(dto.FlowName))
            {
                var def = Context.Queryable<WfFlowDefinition>().First(d => d.FlowId == inst.FlowId);
                if (def != null) dto.FlowName = def.FlowName;
            }
            if (inst.CurrentNodeId.HasValue)
            {
                var node = Context.Queryable<WfFlowNode>().First(n => n.NodeId == inst.CurrentNodeId);
                if (node != null) dto.CurrentNodeName = node.NodeName;
            }
            dto.Tasks = Context.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == instanceId)
                .OrderBy(t => t.TaskId)
                .ToList()
                .Adapt<List<WfFlowTaskDto>>();
            dto.Records = Context.Queryable<WfFlowRecord>()
                .Where(r => r.InstanceId == instanceId)
                .OrderBy(r => r.RecordId)
                .ToList()
                .Adapt<List<WfFlowRecordDto>>();
            // 申请人/操作人昵称已在落库时快照（ApplyNickName / OperatorNickName），直接随 DTO 返回，无需运行时关联用户表
            return dto;
        }

        public long Start(WfFlowInstanceDto dto, LoginUser user)
        {
            var instance = dto.Adapt<WfFlowInstance>();
            instance.ApplyUser = user.UserName;
            instance.ApplyUserId = user.UserId;
            instance.Status = (int)WfInstanceStatus.Approval;
            instance.Create_by = user.UserName;
            instance.Create_time = DateTime.Now;
            return _engine.Start(instance);
        }

        /// <summary>
        /// 驳回后重新提交：申请人修改内容再次发起，回到首节点重新审批
        /// </summary>
        public void Resubmit(long instanceId, WfFlowInstanceDto dto, string userName)
        {
            _engine.Resubmit(instanceId, dto.FormContent, dto.Attachment, dto.Title, userName);
        }

        /// <summary>
        /// 数据面板统计：聚合当前用户的待办/已办/我发起/抄送数量。
        /// 全部为只读 Count，单次调用返回所有指标。
        /// </summary>
        public WfDashboardStatsDto GetDashboardStats(long userId)
        {
            // 待办/已办：按状态分组一次聚合，减少数据库往返
            var taskStats = Context.Queryable<WfFlowTask>()
                .Where(t => t.AssigneeId == userId)
                .GroupBy(t => t.Status)
                .Select(t => new { Status = t.Status, Cnt = SqlFunc.AggregateCount(1) })
                .ToList();
            var todoCount = taskStats.FirstOrDefault(x => x.Status == (int)WfTaskStatus.Pending)?.Cnt ?? 0;
            var doneCount = taskStats.FirstOrDefault(x => x.Status == (int)WfTaskStatus.Done)?.Cnt ?? 0;

            // 我发起：按状态分组一次聚合
            var instStats = Context.Queryable<WfFlowInstance>()
                .Where(i => i.ApplyUserId == userId)
                .GroupBy(i => i.Status)
                .Select(i => new { Status = i.Status, Cnt = SqlFunc.AggregateCount(1) })
                .ToList();
            var myInProgress = instStats.FirstOrDefault(x => x.Status == (int)WfInstanceStatus.Approval)?.Cnt ?? 0;
            var myCompleted = instStats
                .Where(x => x.Status == (int)WfInstanceStatus.Approved || x.Status == (int)WfInstanceStatus.Rejected)
                .Sum(x => x.Cnt);

            // 抄送：记录已按收件人拆分并写入 OperatorId，且 Action 标记为 WfAction.Cc，直接按 userId 精确匹配，无需反查用户表
            var ccCount = Context.Queryable<WfFlowRecord>()
                .Count(r => r.Action == (int)WfAction.Cc && r.OperatorId == userId);

            return new WfDashboardStatsDto
            {
                TodoCount = todoCount,
                DoneCount = doneCount,
                MyInProgress = myInProgress,
                MyCompleted = myCompleted,
                CcCount = ccCount
            };
        }

        /// <summary>
        /// 流程效率统计（基于当前用户作为申请人的实例）：
        /// 1) 平均/最短/最长审批时长：已通过实例的 Update_time(完成) - Create_time(发起)；
        /// 2) 各节点平均耗时：已完成任务 HandleTime - Create_time，按节点名称聚合；
        /// 3) 完成率趋势：按月统计结束实例（通过+驳回），通过数 / 结束总数。
        /// </summary>
        public WfEfficiencyStatsDto GetEfficiencyStats(long userId)
        {
            // 已完成（通过）实例：用 Update_time 近似完成时间（引擎在状态流转时更新）
            var finished = Context.Queryable<WfFlowInstance>()
                .Where(i => i.ApplyUserId == userId && i.Status == (int)WfInstanceStatus.Approved)
                .Select(i => new { i.Create_time, i.Update_time })
                .ToList();

            var durations = finished
                .Select(x => (decimal)((x.Update_time ?? x.Create_time) - x.Create_time).TotalHours)
                .ToList();

            var eff = new WfEfficiencyStatsDto
            {
                FinishedCount = finished.Count
            };
            if (durations.Count > 0)
            {
                eff.AvgDurationHours = (decimal)Math.Round((double)durations.Average(), 2);
                eff.MinDurationHours = (decimal)Math.Round((double)durations.Min(), 2);
                eff.MaxDurationHours = (decimal)Math.Round((double)durations.Max(), 2);
            }

            // 各节点耗时分布：仅统计已处理(Status=Done)且 HandleTime 有值的任务
            var nodeDurations = Context.Queryable<WfFlowTask>()
                .Where(t => t.AssigneeId == userId && t.Status == (int)WfTaskStatus.Done && t.HandleTime != null)
                .GroupBy(t => t.NodeName)
                .Select(t => new
                {
                    NodeName = t.NodeName,
                    AvgHours = SqlFunc.AggregateAvg(SqlFunc.DateDiff(DateType.Hour, t.Create_time, t.HandleTime.Value)),
                    Cnt = SqlFunc.AggregateCount(1)
                })
                .ToList()
                .Where(x => !string.IsNullOrEmpty(x.NodeName))
                .Select(x => new WfNodeDurationDto
                {
                    NodeName = x.NodeName,
                    AvgHours = (decimal)Math.Round((double)x.AvgHours, 2),
                    Count = x.Cnt
                })
                .OrderByDescending(x => x.AvgHours)
                .ToList();
            eff.NodeDurations = nodeDurations;

            // 完成率趋势：按月统计结束实例（通过 + 驳回）
            var ended = Context.Queryable<WfFlowInstance>()
                .Where(i => i.ApplyUserId == userId && (i.Status == (int)WfInstanceStatus.Approved || i.Status == (int)WfInstanceStatus.Rejected))
                .Select(i => new { i.Status, i.Update_time })
                .ToList();

            eff.CompletionTrend = ended
                .GroupBy(x => (x.Update_time ?? DateTime.Now).ToString("yyyy-MM"))
                .Select(g => new WfCompletionTrendDto
                {
                    Month = g.Key,
                    TotalFinished = g.Count(),
                    Approved = g.Count(x => x.Status == (int)WfInstanceStatus.Approved),
                    Rejected = g.Count(x => x.Status == (int)WfInstanceStatus.Rejected)
                })
                .Select(g => new WfCompletionTrendDto
                {
                    Month = g.Month,
                    TotalFinished = g.TotalFinished,
                    Approved = g.Approved,
                    Rejected = g.Rejected,
                    Rate = g.TotalFinished == 0 ? 0 : Math.Round((decimal)g.Approved * 100 / g.TotalFinished, 1)
                })
                .OrderBy(g => g.Month)
                .ToList();

            return eff;
        }
    }
}
