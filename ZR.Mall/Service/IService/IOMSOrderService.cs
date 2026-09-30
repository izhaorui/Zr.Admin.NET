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
        /// 未发货订单数
        /// </summary>
        int NotDelivereOrder();

        /// <summary>
        /// 订单各状态计数统计（列表顶部概览卡）
        /// </summary>
        Dictionary<int, int> GetOrderStatusStats();

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
        /// 导出订单
        /// </summary>
        PagedInfo<OMSOrderDto> ExportList(OMSOrderQueryDto parm);

        /// <summary>
        /// 导出待发货订单（物流面单）
        /// </summary>
        Task<List<DeliveryExpressDto>> ExportWaitDeliveryList(OMSOrderQueryDto parm);

        /// <summary>
        /// 总销售额与订单数
        /// </summary>
        Task<dynamic> GetTotalSales(OMSOrderQueryDto dto);

        /// <summary>
        /// 销售趋势（按天）
        /// </summary>
        Task<dynamic> GetSaleTreandByDay(OMSOrderQueryDto dto);

        /// <summary>
        /// 销售排名前 10 的商品
        /// </summary>
        Task<dynamic> GetSaleTopProduct(OMSOrderQueryDto dto);
    }
}
