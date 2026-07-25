using Infrastructure.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZR.Mall.Enum;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

//创建时间：2026-07-25
namespace ZR.Mall.Controllers
{
    /// <summary>
    /// 商城C端/游客商品浏览接口（匿名可访问，与后台 shopping/product 区分）
    /// 仅返回已上架商品，不暴露管理字段
    /// </summary>
    [Route("shopping/front/product")]
    [ApiExplorerSettings(GroupName = "shopping")]
    [AllowAnonymous]
    public class FrontProductController : BaseController
    {
        private readonly IProductService _ShoppingProductService;

        public FrontProductController(IProductService ShoppingProductService)
        {
            _ShoppingProductService = ShoppingProductService;
        }

        /// <summary>
        /// 游客商品列表（仅上架商品）
        /// </summary>
        [HttpGet("list")]
        public IActionResult QueryShoppingProduct([FromQuery] ShoppingProductQueryDto parm)
        {
            parm ??= new ShoppingProductQueryDto();
            parm.SaleStatus = SaleStatus.OnSale; // 游客只能看在售商品
            var response = _ShoppingProductService.GetList(parm);
            return SUCCESS(response);
        }

        /// <summary>
        /// 游客商品详情（仅上架商品）
        /// </summary>
        [HttpGet("{ProductId}")]
        public IActionResult GetShoppingProduct(long ProductId)
        {
            var response = _ShoppingProductService.GetInfo(ProductId);
            if (response == null || response.SaleStatus != SaleStatus.OnSale)
            {
                return ToResponse(ResultCode.FAIL, "商品不存在或已下架");
            }
            return SUCCESS(response);
        }
    }
}
