using ZR.Model.Public.Dto;

namespace ZR.Mall.Service.IService
{
    /// <summary>
    /// 商城 C 端首页服务（匿名可访问）。
    /// 与后台的 IBannerConfigService 区分：这里只暴露首页需要的只读数据。
    /// </summary>
    public interface IFrontHomeService
    {
        /// <summary>
        /// 首页轮播 Banner（广告位 AdType=99）：仅「展示中且在有效期内」的记录，按 SortId 升序。
        /// </summary>
        List<BannerConfigDto> GetHomeBanners();
    }
}
