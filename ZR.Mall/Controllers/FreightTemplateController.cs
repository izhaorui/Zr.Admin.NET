using Microsoft.AspNetCore.Mvc;
using ZR.Common;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Controllers
{
    /// <summary>
    /// 运费模板管理
    /// </summary>
    /// <remarks>
    /// 权限前缀统一 <c>shop:freight:*</c>，与菜单种子（<c>MallModuleInitProvider</c>）一一对应。
    /// </remarks>
    [Route("shopping/freightTemplate")]
    [ApiExplorerSettings(GroupName = "shopping")]
    public class FreightTemplateController : BaseController
    {
        private readonly IFreightTemplateService _freightTemplateService;

        public FreightTemplateController(IFreightTemplateService freightTemplateService)
        {
            _freightTemplateService = freightTemplateService;
        }

        /// <summary>
        /// 查询运费模板列表
        /// </summary>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "shop:freight:list")]
        public IActionResult QueryFreightTemplate([FromQuery] FreightTemplateQueryDto parm)
        {
            return SUCCESS(_freightTemplateService.GetList(parm));
        }

        /// <summary>
        /// 全部模板（商品编辑页下拉选项）
        /// </summary>
        /// <remarks>不套 list 权限：商品编辑页也要用，挂在 freight 权限下会让编辑商品受限</remarks>
        [HttpGet("all")]
        [ActionPermissionFilter(Permission = "shop:freight:list,shop:product:list,shop:product:edit")]
        public IActionResult ListAll()
        {
            return SUCCESS(_freightTemplateService.ListAll());
        }

        /// <summary>
        /// 查询运费模板详情（含不配送地区）
        /// </summary>
        [HttpGet("{Id}")]
        [ActionPermissionFilter(Permission = "shop:freight:query")]
        public IActionResult GetFreightTemplate(long Id)
        {
            var info = _freightTemplateService.GetInfo(Id);
            if (info == null)
            {
                return ToResponse(ResultCode.FAIL, "运费模板不存在或已删除");
            }
            return SUCCESS(info);
        }

        /// <summary>
        /// 添加运费模板
        /// </summary>
        [HttpPost]
        [ActionPermissionFilter(Permission = "shop:freight:add")]
        [Log(Title = "运费模板", BusinessType = BusinessType.INSERT)]
        public IActionResult AddFreightTemplate([FromBody] FreightTemplateInputDto parm)
        {
            return SUCCESS(_freightTemplateService.AddTemplate(parm));
        }

        /// <summary>
        /// 更新运费模板
        /// </summary>
        [HttpPut]
        [ActionPermissionFilter(Permission = "shop:freight:edit")]
        [Log(Title = "运费模板", BusinessType = BusinessType.UPDATE)]
        public IActionResult UpdateFreightTemplate([FromBody] FreightTemplateInputDto parm)
        {
            if (parm?.Id == null || parm.Id <= 0)
            {
                return ToResponse(ResultCode.PARAM_ERROR, "模板 Id 不能为空");
            }
            return ToResponse(_freightTemplateService.UpdateTemplate(parm));
        }

        /// <summary>
        /// 删除运费模板
        /// </summary>
        [HttpPost("delete/{ids}")]
        [ActionPermissionFilter(Permission = "shop:freight:delete")]
        [Log(Title = "运费模板", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteFreightTemplate([FromRoute] string ids)
        {
            var idArr = Tools.SplitAndConvert<long>(ids);
            return ToResponse(_freightTemplateService.DeleteTemplates(idArr));
        }
    }
}
