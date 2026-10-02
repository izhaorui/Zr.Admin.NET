using ZR.Mall.Model;
using ZR.Mall.Model.Dto;

namespace ZR.Mall.Service.IService
{
    /// <summary>
    /// 售后单 service 接口
    /// </summary>
    /// <remarks>
    /// 流程：买家申请 → 后台审核 →（退货退款：买家寄回 + 商家确认收货）→ 运营登记渠道退款 → 完成。
    /// 渠道退款由人工在支付渠道完成后回到这里登记，与项目既有约定一致。
    /// </remarks>
    public interface IOMSAfterSaleService : IBaseService<OMSAfterSale>
    {
        /// <summary>
        /// 买家申请售后（校验归属、订单可退状态与可退余额，同一订单同时只允许一笔进行中）
        /// </summary>
        /// <returns>新建售后单 Id</returns>
        long Apply(long memberId, AfterSaleApplyDto dto);

        /// <summary>
        /// 撤销售后申请（买家传 memberId 做归属校验；后台传 null 表示代客撤销）。
        /// 仅「待审核 / 待买家退货 / 待商家收货」可撤销 —— 进入退款环节后钱已在退，撤销无从生效。
        /// </summary>
        int Cancel(long id, long? memberId);

        /// <summary>
        /// 后台审核：通过则按类型进入「待买家退货」或「待退款」，驳回则终态。
        /// </summary>
        int Audit(AfterSaleAuditDto dto);

        /// <summary>
        /// 买家填写退货物流单号（待买家退货 → 待商家收货）
        /// </summary>
        int FillReturnLogistics(long memberId, AfterSaleReturnDto dto);

        /// <summary>
        /// 商家确认收到退货（待商家收货 → 待退款）
        /// </summary>
        int ConfirmReceive(long id, string remark);

        /// <summary>
        /// 登记渠道退款并完成售后（待退款 → 已完成）：
        /// 写入实际退款金额与渠道退款单号，并把退款落账到订单（累计 RefundAmount、置退款状态、按需回补库存）。
        /// </summary>
        int RegisterRefund(AfterSaleRefundDto dto);

        /// <summary>后台售后列表（含订单实付/已退/商品摘要）</summary>
        PagedInfo<AfterSaleDto> GetList(AfterSaleQueryDto parm);

        /// <summary>后台售后详情</summary>
        AfterSaleDto GetDetail(long id);

        /// <summary>C 端「我的售后」列表（强制按该会员过滤）</summary>
        PagedInfo<AfterSaleDto> GetMemberList(long memberId, AfterSaleQueryDto parm);

        /// <summary>C 端售后详情（校验归属）</summary>
        AfterSaleDto GetMemberDetail(long id, long memberId);
    }
}
