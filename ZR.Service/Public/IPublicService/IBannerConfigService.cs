using ZR.Model.Public.Dto;
using ZR.Model.Public;

namespace ZR.Service.Public.IPublicService
{
    /// <summary>
    /// 广告管理service接口
    /// </summary>
    public interface IBannerConfigService : IBaseService<BannerConfig>
    {
        PagedInfo<BannerConfigDto> GetList(BannerConfigQueryDto parm);

        BannerConfig GetInfo(int Id);

        BannerConfig AddBannerConfig(BannerConfig parm);
        int UpdateBannerConfig(BannerConfig parm);

        List<BannerConfigDto> GetBannerList(BannerConfigQueryDto parm);
        PagedInfo<BannerConfigDto> ExportList(BannerConfigQueryDto parm);
    }
}
