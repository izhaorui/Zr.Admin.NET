using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Model.Public;

namespace ZR.Mall.Service.IService
{
    /// <summary>
    /// 行政区划service接口
    /// </summary>
    public interface IAreaService : IBaseService<Area>
    {
        /// <summary>
        /// 按父级 Id 取直接下级（后台级联下拉懒加载）
        /// </summary>
        List<AreaTreeDto> GetChildren(long parentId);

        /// <summary>
        /// 按编码取地区节点；未命中返回 null
        /// </summary>
        Area GetByCode(string areaCode);

        /// <summary>
        /// 批量按编码取地区节点，返回「编码 → 节点」字典
        /// </summary>
        Dictionary<string, Area> GetByCodes(IEnumerable<string> areaCodes);

        /// <summary>
        /// 解析收货地区：逐级「编码优先、失效则按名称反查」，并回填字典标准名
        /// </summary>
        AreaCodesDto ResolveCodes(AreaQueryDto query);
    }

    /// <summary>
    /// 运费编排service接口
    /// </summary>
    /// <remarks>
    /// C 端试算与下单主流程必须都走本方法，不要各自实现一套 ——
    /// 否则会出现「试算 8 元、提交变 10 元」，是用户最常见也最难解释的投诉。
    /// </remarks>
    public interface IFreightService : IBaseService<FreightTemplate>
    {
        /// <summary>
        /// 按「收货地区 + 商品行」计算运费
        /// </summary>
        /// <param name="items">商品行（skuId + 数量）</param>
        /// <param name="goodsAmount">商品总额（整单口径，不含运费）</param>
        /// <param name="address">收货地区（名称 + 可选编码；编码缺失会按名称兜底反查）</param>
        /// <returns>计算结果，含合计运费、是否包邮与按模板分段的明细</returns>
        FreightCalcResult CalculateForCheckout(List<FreightCheckoutItem> items,
            decimal goodsAmount, AreaQueryDto address);

        /// <summary>
        /// 批量取多个商品的包邮信息（C 端列表与详情共用）
        /// </summary>
        /// <param name="products">商品 Id 与该商品绑定的运费模板 Id（null 表示未绑定）</param>
        /// <returns>商品 Id → 包邮信息；未绑定或模板已被删的商品按默认模板折算</returns>
        /// <remarks>
        /// 模板回落与 <see cref="CalculateForCheckout"/> 同源（<c>FreightCalculator.PickTemplate</c>），
        /// 否则会出现「详情页显示包邮、下单却收运费」。
        /// 无论商品多少个都只查 2 次库（绑定的模板一批 + 默认模板一次），列表页不会退化成 N+1。
        /// 只给「是否包邮 + 门槛」不给金额 —— 列表与详情都拿不到收货地区，也没有整单总额。
        /// </remarks>
        Dictionary<long, FrontFreightInfoDto> ResolveFrontFreightInfoBatch(
            IEnumerable<(long ProductId, long? FreightTemplateId)> products);
    }
}
