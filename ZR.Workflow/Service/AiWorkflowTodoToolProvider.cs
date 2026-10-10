using Infrastructure.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZR.Model.AI.Dto;
using ZR.ServiceCore.AI.IService;
using ZR.Workflow.Model.Dto;
using ZR.Workflow.Service.IService;

namespace ZR.Workflow.Service
{
    /// <summary>
    /// AI 助手扩展工具：审批待办查询。
    /// 通过 IAiAssistantToolProvider 暴露给全局 AI 助手，工具内部调用工作流既有 Service，
    /// 天然带上“仅当前用户待办”的归属约束（GetTodoList 内部按 userId 过滤）。
    /// </summary>
    [AppService(ServiceType = typeof(IAiAssistantToolProvider), ServiceLifetime = LifeTime.Transient)]
    public class AiWorkflowTodoToolProvider : IAiAssistantToolProvider
    {
        private readonly IWfFlowTaskService _taskService;

        public string ProviderName => "workflow";

        public AiWorkflowTodoToolProvider(IWfFlowTaskService taskService)
        {
            _taskService = taskService;
        }

        public List<AiToolDef> GetToolDefs()
        {
            return new List<AiToolDef>
            {
                new AiToolDef
                {
                    Name = "query_my_todos",
                    Label = "待办查询",
                    Samples = new List<string>
                    {
                        "我有多少待办？",
                        "待我审批的任务有哪些？"
                    },
                    Description = "查询当前登录用户待我审批的待办任务列表。适用于“我有多少待办”“待我审批的”“我的待办”等提问。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>(),
                        required = Array.Empty<string>()
                    }
                }
            };
        }

        public async Task<AiToolExecResult> ExecuteAsync(string toolName, string argsJson, long userId)
        {
            if (toolName != "query_my_todos")
            {
                return null;
            }
            var page = await _taskService.GetTodoListAsync(new WfFlowTaskQueryDto { PageNum = 1, PageSize = 20 }, userId);
            var list = page?.Result;
            if (list == null || list.Count == 0)
            {
                return AiToolExecResult.Success("当前没有待我审批的待办任务。");
            }
            var lines = list.Select(x =>
            {
                var time = x.Create_time.ToString("MM-dd HH:mm");
                return $"- [{x.FlowName}] {x.Title}（{x.NodeName}，申请人 {x.ApplyNickName}，{time}）";
            });
            return AiToolExecResult.Success(
                $"查询到 {list.Count} 条待办审批任务：\n" + string.Join("\n", lines));
        }
    }
}
