using ZR.Mall.Model;
using ZR.Mall.Model.Dto;

namespace ZR.Mall.Service.IService
{
    /// <summary>
    /// 后台订单管理 service 接口（运营侧）：查询列表、概览统计、修改/备注、导出、销售统计。
    ///
    /// 状态迁移（发货、取消、退款、支付）不在这里，见 <see cref="IOMSOrderStateService"/>；
    /// C 端交易（下单、我的订单、会员取消）见 <see cref="IFrontOrderService"/>。
    /// 本接口继承 IBaseService，因为后台列表页还需要 Queryable/Deleteable/GetByIdAsync 等基类能力。
    /// </summary>
    public interface IOMSOrderService : IBaseService<OMSOrder>
    {
        /// <summary>
        /// 后台订单分页列表
        /// </summary>
        PagedInfo<OMSOrderDto> GetList(OMSOrderQueryDto parm);

        /// <summary>
        /// 订单详情
        /// </summary>
        OMSOrder GetInfo(long Id);

        /// <summary>
        /// 订单中**敏感字段**的完整值（揭示接口专用）：只返回敏感字段，非敏感字段本就是明文。
        /// 调用方必须同时具备独立权限 <c>oms:order:sensitive</c> 与操作日志，否则等于开放明文。
        /// </summary>
        List<OrderFormValue> GetSensitiveFormValues(long orderId);

        /// <summary>
        /// 订单概览统计（列表顶部概览卡）：各状态计数与"待发货未发货"数一次查询得出。
        /// </summary>
        OrderOverviewDto GetOrderOverview();

        /// <summary>
        /// 修改订单（operType 取值见 <see cref="ZR.Mall.Enum.OrderUpdateOperType"/>：
        /// 1=通用更新 / 2=修改平台备注 / 3=修改收货地址 / 4=退款；未识别的值会抛出业务异常）
        /// </summary>
        int UpdateOMSOrder(int operType, OMSOrder parm);

        /// <summary>
        /// 修改商家备注
        /// </summary>
        int UpdateMerchantNote(OMSOrder model);

        /// <summary>
        /// 软删除订单。待付款/待发货会先按取消规则回补库存，避免逻辑删除后锁定库存无法释放。
        /// </summary>
        int DeleteOrders(long[] ids);

        /// <summary>
        /// 导出订单
        /// </summary>
        PagedInfo<OMSOrderDto> ExportList(OMSOrderQueryDto parm);

        /// <summary>
        /// 导出待发货订单（打单用）：补齐收件人/手机号/收货地区/详细地址/商品明细。
        /// </summary>
        /// <remarks>
        /// 收件信息明文不脱敏 —— 这份文件就是给打单软件用的，真实号码是业务必需。
        /// 导入模板仍用 <see cref="DeliveryExpressDto"/>（三列），两者刻意分开，勿混用。
        /// </remarks>
        Task<List<DeliveryExportDto>> ExportWaitDeliveryList(OMSOrderQueryDto parm);

        /// <summary>
        /// 总销售额与订单数
        /// </summary>
        Task<SalesTotalDto> GetTotalSales(OMSOrderQueryDto dto);

        /// <summary>
        /// 销售趋势（按天）
        /// </summary>
        Task<dynamic> GetSaleTreandByDay(OMSOrderQueryDto dto);

        /// <summary>
        /// 销售排名前 10 的商品
        /// </summary>
        Task<dynamic> GetSaleTopProduct(OMSOrderQueryDto dto);

        /// <summary>
        /// 经营概览：客单价 / 退款率 / 售后率 / 形态占比 / 复购率。
        /// 时间范围按支付时间，与销售总额同口径（见 <see cref="Common.MallStatsScope"/>）。
        /// </summary>
        Task<BusinessOverviewDto> GetBusinessOverview(OMSOrderQueryDto dto);

        /// <summary>
        /// 退款 Top10 商品。口径与销售统计**故意不同**（退款单多为 Closed，按有效订单口径过滤会恒空），
        /// 详见实现注释。
        /// </summary>
        Task<List<RefundTopProductDto>> GetRefundTopProduct(OMSOrderQueryDto dto);
    }
}
