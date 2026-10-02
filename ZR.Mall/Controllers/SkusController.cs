using Microsoft.AspNetCore.Mvc;
using ZR.Common;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

//创建时间：2025-05-24
namespace ZR.Mall.Controllers
{
    /// <summary>
    /// 商品库存
    /// </summary>
    [Route("shopping/skus")]
    [ApiExplorerSettings(GroupName = "shopping")]
    public class SkusController : BaseController
    {
        /// <summary>
        /// 商品库存接口
        /// </summary>
        private readonly ISkusService _ShoppingSkusService;

        public SkusController(ISkusService ShoppingSkusService)
        {
            _ShoppingSkusService = ShoppingSkusService;
        }

        /// <summary>
        /// 查询商品库存列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "shop:skus:list")]
        public IActionResult QueryShoppingSkus([FromQuery] ShoppingSkusQueryDto parm)
        {
            var response = _ShoppingSkusService.GetList(parm);
            return SUCCESS(response);
        }

        /// <summary>
        /// 查询商品库存详情
        /// </summary>
        /// <param name="SkuId"></param>
        /// <returns></returns>
        [HttpGet("{SkuId}")]
        [ActionPermissionFilter(Permission = "shop:skus:query")]
        public IActionResult GetShoppingSkus(int SkuId)
        {
            var response = _ShoppingSkusService.GetInfo(SkuId);
            if (response == null)
            {
                return ToResponse(ResultCode.FAIL, "商品规格不存在");
            }

            var info = response.Adapt<SkusDto>();
            return SUCCESS(info);
        }

        /// <summary>
        /// 添加商品库存
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ActionPermissionFilter(Permission = "shop:skus:add")]
        [Log(Title = "商品库存", BusinessType = BusinessType.INSERT)]
        public IActionResult AddShoppingSkus([FromBody] SkusDto parm)
        {
            var modal = parm.Adapt<Skus>().ToCreate(HttpContext);

            var response = _ShoppingSkusService.AddShoppingSkus(modal);

            return SUCCESS(response);
        }

        /// <summary>
        /// 更新商品库存
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [ActionPermissionFilter(Permission = "shop:skus:edit")]
        [Log(Title = "商品库存", BusinessType = BusinessType.UPDATE)]
        public IActionResult UpdateShoppingSkus([FromBody] SkusDto parm)
        {
            var modal = parm.Adapt<Skus>().ToUpdate(HttpContext);
            var response = _ShoppingSkusService.UpdateShoppingSkus(modal);

            return ToResponse(response);
        }

        /// <summary>
        /// 删除商品库存
        /// </summary>
        /// <returns></returns>
        [HttpPost("delete/{ids}")]
        [ActionPermissionFilter(Permission = "shop:skus:delete")]
        [Log(Title = "商品库存", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteShoppingSkus([FromRoute]string ids)
        {
            // 走 Service 而非基类 Delete：删除后需要回写所属商品的价格区间/规格摘要
            return ToResponse(_ShoppingSkusService.DeleteShoppingSkus(ids));
        }
    }
}