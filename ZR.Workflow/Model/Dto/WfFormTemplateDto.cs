namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 表单模板查询对象
    /// </summary>
    public class WfFormTemplateQueryDto : PagerInfo
    {
        /// <summary>模板名称</summary>
        public string FormName { get; set; }
        /// <summary>状态</summary>
        public int? Status { get; set; }
    }

    /// <summary>
    /// 表单模板输入输出对象
    /// </summary>
    public class WfFormTemplateDto : SysBase
    {
        /// <summary>模板Id</summary>
        public long FormId { get; set; }

        /// <summary>模板名称</summary>
        [Required(ErrorMessage = "模板名称不能为空")]
        public string FormName { get; set; }

        /// <summary>状态 0=停用 1=启用</summary>
        public int Status { get; set; } = 1;

        /// <summary>表单字段定义（JSON）</summary>
        public string FormItems { get; set; }
    }
}
