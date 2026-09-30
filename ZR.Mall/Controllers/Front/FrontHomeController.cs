using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Controllers.Front
{
    /// <summary>
    /// 商城 C 端首页接口（匿名可访问，与后台 public/BannerConfig 区分）。
    /// 前端首页轮播直接调本接口，不直连后台的 bannerList。
    /// </summary>
    [PublicApi]
    [Route("shopping/front/home")]
    [ApiExplorerSettings(GroupName = "shopping")]
    [AllowAnonymous]
    public class FrontHomeController : BaseController
    {
        private readonly IFrontHomeService _frontHomeService;

        public FrontHomeController(IFrontHomeService frontHomeService)
        {
            _frontHomeService = frontHomeService;
        }

        /// <summary>
        /// 首页轮播 Banner（广告位 AdType=99，仅启用且在有效期内的记录，按 SortId 升序）。
        /// </summary>
        [HttpGet("banner")]
        public IActionResult GetHomeBanner()
        {
            return SUCCESS(_frontHomeService.GetHomeBanners());
        }
    }
}
