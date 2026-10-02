using ZR.Mall.Model;
using ZR.Mall.Model.Dto;

namespace ZR.Mall.Service.IService
{
    /// <summary>
    /// 运费模板service接口
    /// </summary>
    public interface IFreightTemplateService : IBaseService<FreightTemplate>
    {
        /// <summary>分页查询模板列表（含已绑定商品数）</summary>
        PagedInfo<FreightTemplateDto> GetList(FreightTemplateQueryDto parm);

        /// <summary>查询模板详情（含不配送地区）</summary>
        FreightTemplateDto GetInfo(long id);

        /// <summary>全部模板（商品编辑页的下拉选项）</summary>
        List<FreightTemplateDto> ListAll();

        /// <summary>新增模板</summary>
        long AddTemplate(FreightTemplateInputDto parm);

        /// <summary>修改模板（含覆盖式保存不配送地区）</summary>
        int UpdateTemplate(FreightTemplateInputDto parm);

        /// <summary>删除模板；被商品引用时抛异常拒绝</summary>
        int DeleteTemplates(long[] ids);
    }
}
