using ZR.Workflow.Model.Dto;

namespace ZR.Workflow.Service.IService
{
    /// <summary>
    /// 表单模板服务：可复用动态表单的保存与查询
    /// </summary>
    public interface IWfFormTemplateService
    {
        PagedInfo<WfFormTemplateDto> GetList(WfFormTemplateQueryDto parm);
        WfFormTemplateDto GetInfo(long formId);
        WfFormTemplate Add(WfFormTemplateDto dto);
        int Update(WfFormTemplateDto dto);
        int Delete(long[] ids);
    }
}
