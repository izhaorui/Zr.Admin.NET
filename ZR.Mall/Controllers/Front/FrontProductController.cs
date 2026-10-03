using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZR.Mall.Enum;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

//创建时间：2026-07-25
namespace ZR.Mall.Controllers.Front
{
    /// <summary>
    /// 商城 C 端商品浏览接口（匿名可访问，与后台 shopping/product 区分）
    /// 仅返回已上架商品，不暴露管理字段
    /// </summary>
    [PublicApi]
    [Route("shopping/front/product")]
    [ApiExplorerSettings(GroupName = "shopping")]
    [AllowAnonymous]
    public class FrontProductController : BaseController
    {
        private readonly IProductService _ShoppingProductService;
        private readonly ICategoryService _CategoryService;
        private readonly IFreightService _freightService;

        public FrontProductController(IProductService ShoppingProductService, ICategoryService categoryService,
            IFreightService freightService)
        {
            _ShoppingProductService = ShoppingProductService;
            _CategoryService = categoryService;
            _freightService = freightService;
        }

        /// <summary>
        /// 匿名商品列表（仅上架商品）
        /// </summary>
        [HttpGet("list")]
        public IActionResult QueryShoppingProduct([FromQuery] ShoppingProductQueryDto parm)
        {
            parm ??= new ShoppingProductQueryDto();
            parm.SaleStatus = SaleStatus.OnSale; // 匿名访客只能看在售商品
            var response = _ShoppingProductService.GetFrontList(parm);
            FillFreightInfo(response.Result);
            return SUCCESS(response);
        }

        /// <summary>
        /// 匿名商品详情（仅上架商品）
        /// </summary>
        [HttpGet("{ProductId}")]
        public IActionResult GetShoppingProduct(long ProductId)
        {
            var response = _ShoppingProductService.GetInfo(ProductId);
            if (response == null || response.SaleStatus != SaleStatus.OnSale)
            {
                return ToResponse(ResultCode.FAIL, "商品不存在或已下架");
            }

            FillFreightInfo(new[] { response });
            return SUCCESS(response);
        }

        /// <summary>
        /// 批量回填包邮标签（列表与详情共用）。
        /// </summary>
        /// <remarks>
        /// 两处走同一个入口，同一个商品不会出现「列表说包邮、详情说不包邮」；
        /// 且无论多少商品都只查 2 次库，列表页不会退化成 N+1。
        /// 只回「是否包邮 + 门槛」不回金额：两处都拿不到收货地区，列表更不知道整单总额。
        /// </remarks>
        private void FillFreightInfo(IEnumerable<ProductDto> products)
        {
            var list = products?.ToList();
            if (list == null || list.Count == 0)
            {
                return;
            }

            // 虚拟商品不回运费信息（FreightInfo 留 null，C 端标签自然不显示）；形态判断只在这一处
            var infoMap = _freightService.ResolveFrontFreightInfoBatch(
                list.Where(x => x.ProductType == ProductTypeEnum.Physical)
                    .Select(x => (x.ProductId, x.FreightTemplateId)));
            foreach (var product in list)
            {
                if (infoMap.TryGetValue(product.ProductId, out var info))
                {
                    product.FreightInfo = info;
                }
            }
        }

        /// <summary>
        /// 匿名可见的商品分类列表，用于 C 端首页分类筛选。
        /// 仅返回 ShowStatus=1（展示中）且未删除的分类，按 OrderNum 升序；过滤与缓存均在 Service 内完成。
        /// </summary>
        [HttpGet("category/list")]
        public IActionResult QueryFrontCategory()
        {
            var list = _CategoryService.GetFrontCategories()
                .Select(c => new { categoryId = c.CategoryId, name = c.Name, icon = c.Icon })
                .ToList();
            return SUCCESS(list);
        }
    }
}
