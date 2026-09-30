using ZR.Mall.Service.IService;
using ZR.Model.Public;
using ZR.Model.Public.Dto;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 商城 C 端首页服务实现。
    ///
    /// 说明：BannerConfig 无 [Tenant] 特性，BaseRepository 会落到主库（MainDbConfigId），
    /// 与后台 IBannerConfigService 查的是同一张 banner_config 表；非 SaaS 下主库与商城库同库。
    /// </summary>
    [AppService(ServiceType = typeof(IFrontHomeService))]
    public class FrontHomeService : BaseService<BannerConfig>, IFrontHomeService
    {
        /// <summary>
        /// 首页轮播广告位类型（AdType）。约定：99 = 商城首页轮播。
        /// </summary>
        public const int HomeBannerAdType = 99;

        /// <summary>
        /// 首页轮播 Banner：ShowStatus=0（显示）+ AdType=99 + 有效期内，按 SortId 升序。
        /// 时间边界为空视为「不限」，避免未配置起止时间的 Banner 被静默过滤掉。
        ///
        /// 缓存：读多写极少，走 SqlSugar 查询缓存（后端由 RedisServer:dbCache 决定 Redis 或内存）。
        /// 后台增删改 banner_config 时 IsAutoRemoveDataCache=true 会自动清缓存，无需手动失效；
        /// TTL 仅作兜底（例如直接用 SQL 改库不会触发自动失效）。
        /// </summary>
        public List<BannerConfigDto> GetHomeBanners()
        {
            var now = DateTime.Now;

            return Queryable()
                .WithCache(60 * 30)
                .Where(x => x.AdType == HomeBannerAdType && x.ShowStatus == 0)
                .Where(x => (x.BeginTime == null || x.BeginTime <= now)
                         && (x.EndTime == null || x.EndTime >= now))
                .OrderBy(x => x.SortId)
                .ToList()
                .Adapt<List<BannerConfigDto>>();
        }
    }
}
