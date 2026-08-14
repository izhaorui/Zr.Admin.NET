namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 流程定义查询对象
    /// </summary>
    public class WfFlowDefinitionQueryDto : PagerInfo
    {
        /// <summary>流程名称</summary>
        public string FlowName { get; set; }
        /// <summary>状态</summary>
        public int? Status { get; set; }
        /// <summary>是否草稿（查询用）0=已发布 1=草稿；为空表示不过滤</summary>
        public int? IsDraft { get; set; }
    }

    /// <summary>
    /// 流程定义输入输出对象
    /// </summary>
    public class WfFlowDefinitionDto : SysBase
    {
        /// <summary>流程定义Id</summary>
        public long FlowId { get; set; }

        /// <summary>流程编码</summary>
        [Required(ErrorMessage = "流程编码不能为空")]
        public string FlowCode { get; set; }

        /// <summary>版本号（同一 FlowCode 下自增，历史版本冻结保留）</summary>
        public int Version { get; set; } = 1;

        /// <summary>流程名称</summary>
        [Required(ErrorMessage = "流程名称不能为空")]
        public string FlowName { get; set; }

        /// <summary>表单类型</summary>
        public int FormType { get; set; } = 0;

        /// <summary>设计器类型 1=LogicFlow 2=Simple</summary>
        public int DesignType { get; set; } = 1;

        /// <summary>状态</summary>
        public int Status { get; set; } = 1;

        /// <summary>是否草稿 0=已发布 1=草稿</summary>
        public int IsDraft { get; set; } = 0;

        /// <summary>是否现行版本（同 FlowCode 下唯一，由查询计算填充，仅展示用）</summary>
        [SugarColumn(IsIgnore = true)]
        public bool IsCurrent { get; set; }

        /// <summary>表单字段定义（JSON）</summary>
        public string FormItems { get; set; }

        /// <summary>LogicFlow 完整设计数据（JSON）</summary>
        public string DesignJson { get; set; }

        /// <summary>节点配置</summary>
        public List<WfFlowNodeDto> Nodes { get; set; } = new();

        /// <summary>节点连线（分支路由）。**前端应始终为每条边生成一条连线**，直线 ConditionJson 留空；引擎据此唯一决定串联走向</summary>
        public List<WfNodeLinkDto> NodeLinks { get; set; } = new();
    }
}
