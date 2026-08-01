using ZR.Workflow.Model.Dto;

namespace ZR.Workflow.Service
{
    /// <summary>
    /// 表单模板服务：可复用动态表单的保存与查询
    /// </summary>
    [AppService(ServiceType = typeof(IWfFormTemplateService))]
    public class WfFormTemplateService : BaseService<WfFormTemplate>, IWfFormTemplateService
    {
        public PagedInfo<WfFormTemplateDto> GetList(WfFormTemplateQueryDto parm)
        {
            var predicate = QueryExp(parm);
            return Queryable().Where(predicate.ToExpression())
                .ToPage<WfFormTemplate, WfFormTemplateDto>(parm);
        }

        public WfFormTemplateDto GetInfo(long formId)
        {
            var entity = Queryable().Where(f => f.IsDelete == 0 && f.FormId == formId).First();
            return entity?.Adapt<WfFormTemplateDto>();
        }

        public WfFormTemplate Add(WfFormTemplateDto dto)
        {
            var entity = dto.Adapt<WfFormTemplate>().ToCreate(App.HttpContext);
            entity = InsertReturnEntity(entity) ?? throw new CustomException("添加表单模板失败");
            return entity;
        }

        public int Update(WfFormTemplateDto dto)
        {
            var existing = Queryable().Where(f => f.FormId == dto.FormId).First();
            if (existing == null)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "表单模板不存在", null);
            var entity = dto.Adapt<WfFormTemplate>().ToUpdate(App.HttpContext);
            return Update(entity, true, "修改表单模板");
        }

        public int Delete(long[] ids)
        {
            // 软删除：仅标记 IsDelete=1，保留历史（若有流程定义已载入该模板，其表单为拷贝快照，不受影响）
            var rows = Context.Updateable<WfFormTemplate>()
                .Where(f => ids.Contains(f.FormId))
                .SetColumns(it => new WfFormTemplate { IsDelete = 1 })
                .ExecuteCommand();
            return rows;
        }

        private static Expressionable<WfFormTemplate> QueryExp(WfFormTemplateQueryDto parm)
        {
            var predicate = Expressionable.Create<WfFormTemplate>();
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.FormName), it => it.FormName.Contains(parm.FormName));
            predicate = predicate.AndIF(parm.Status != null, it => it.Status == parm.Status);
            predicate = predicate.And(it => it.IsDelete == 0);
            return predicate;
        }
    }
}
