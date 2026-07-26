using Microsoft.AspNetCore.Mvc;
using ZR.Common;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

//创建时间：2025-05-29
namespace ZR.Mall.Controllers
{
    /// <summary>
    /// 品牌管理
    /// </summary>
    [Route("shopping/brand")]
    [ApiExplorerSettings(GroupName = "shopping")]
    public class BrandController : BaseController
    {
        /// <summary>
        /// 品牌表接口
        /// </summary>
        private readonly IBrandService _ShopBrandService;

        public BrandController(IBrandService ShopBrandService)
        {
            _ShopBrandService = ShopBrandService;
        }

        /// <summary>
        /// 查询品牌表列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "shop:brand:list")]
        public IActionResult QueryShopBrand([FromQuery] ShopBrandQueryDto parm)
        {
            var response = _ShopBrandService.GetList(parm);
            return SUCCESS(response);
        }

        /// <summary>
        /// 查询品牌表详情
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [HttpGet("{Id}")]
        [ActionPermissionFilter(Permission = "shop:brand:query")]
        public IActionResult GetShopBrand(long Id)
        {
            var response = _ShopBrandService.GetInfo(Id);
            
            var info = response.Adapt<BrandDto>();
            return SUCCESS(info);
        }

        /// <summary>
        /// 添加品牌表
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ActionPermissionFilter(Permission = "shop:brand:add")]
        [Log(Title = "品牌表", BusinessType = BusinessType.INSERT)]
        public IActionResult AddShopBrand([FromBody] BrandDto parm)
        {
            var modal = parm.Adapt<Brand>().ToCreate(HttpContext);

            var response = _ShopBrandService.AddShopBrand(modal);

            return SUCCESS(response);
        }

        /// <summary>
        /// 更新品牌表
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [ActionPermissionFilter(Permission = "shop:brand:edit")]
        [Log(Title = "品牌表", BusinessType = BusinessType.UPDATE)]
        public IActionResult UpdateShopBrand([FromBody] BrandDto parm)
        {
            var modal = parm.Adapt<Brand>().ToUpdate(HttpContext);
            var response = _ShopBrandService.UpdateShopBrand(modal);

            return ToResponse(response);
        }

        /// <summary>
        /// 删除品牌表
        /// </summary>
        /// <returns></returns>
        [HttpPost("delete/{ids}")]
        [ActionPermissionFilter(Permission = "shop:brand:delete")]
        [Log(Title = "品牌表", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteShopBrand([FromRoute]string ids)
        {
            var idArr = Tools.SplitAndConvert<long>(ids);

            return ToResponse(_ShopBrandService.DeleteBrand(idArr));
        }

        /// <summary>
        /// 导出品牌表
        /// </summary>
        /// <returns></returns>
        [Log(Title = "品牌表", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "shop:brand:export")]
        public IActionResult Export([FromQuery] ShopBrandQueryDto parm)
        {
            parm.PageNum = 1;
            parm.PageSize = 100000;
            var list = _ShopBrandService.ExportList(parm).Result;
            if (list == null || list.Count <= 0)
            {
                return ToResponse(ResultCode.FAIL, "没有要导出的数据");
            }
            var result = ExportExcelMini(list, "品牌表", "品牌表");
            return ExportExcel(result.Item2, result.Item1);
        }
    }
}