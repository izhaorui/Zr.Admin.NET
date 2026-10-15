using Microsoft.AspNetCore.Mvc;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Controllers
{
    /// <summary>
    /// 商城 AI 能力：商品营销文案生成。
    /// 挂在商品管理页上，需商品页权限 + AI 按钮权限（shop:product:ai），文案仅返回草稿不落库。
    /// </summary>
    [Route("shopping/ai")]
    [ApiExplorerSettings(GroupName = "shopping")]
    public class MallAiController : BaseController
    {
        private readonly IMallAiService _mallAiService;

        public MallAiController(IMallAiService mallAiService)
        {
            _mallAiService = mallAiService;
        }

        /// <summary>
        /// AI 生成商品文案草稿（标题/卖点/详情描述/SEO 关键词）。仅返回草稿，不写入商品。
        /// </summary>
        [HttpPost("productCopy")]
        [ActionPermissionFilter(Permission = "shop:product:ai")]
        public async Task<IActionResult> ProductCopy([FromBody] MallProductCopyInput parm)
        {
            try
            {
                return SUCCESS(await _mallAiService.GenerateProductCopyAsync(parm));
            }
            catch (Exception ex)
            {
                return ToResponse(ResultCode.FAIL, ex.Message);
            }
        }
    }
}
