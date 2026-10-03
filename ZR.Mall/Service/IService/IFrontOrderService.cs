using ZR.Mall.Model;
using ZR.Mall.Model.Dto;

namespace ZR.Mall.Service.IService
{
    /// <summary>
    /// C 端会员订单交易：下单、支付入口、我的订单、会员取消。
    ///
    /// 与后台运营（IOMSOrderService）、状态内核（IOMSOrderStateService）分离。
    /// 本接口的方法**一律涉及订单归属校验**（memberId / 手机号），是"用户侧"信任模型的入口；
    /// 后台方法则依赖权限过滤器，两者不可混用。
    /// </summary>
    public interface IFrontOrderService : IBaseService<OMSOrder>
    {
        /// <summary>
        /// C 端下单（**必须已登录**）：校验库存、限购、计算金额、写订单与订单项、原子扣库存并累加销量。
        /// memberId 必填（来自 mall-token），订单一律绑定会员；商城已取消游客下单。
        /// 幂等：同一 RequestId（或相同手机号+订单项签名）短窗口内只创建一次订单。
        /// </summary>
        OMSOrder CreateOrder(CreateOrderDto dto, long memberId);

        /// <summary>
        /// 模拟支付（C 端会员直接支付）：校验归属后完成支付（待付款 → 待发货）。
        /// 归属只认会员身份（MemberId）。
        /// </summary>
        OMSOrder PayOrder(string orderNo, long memberId);

        /// <summary>
        /// 会员"我的订单"分页：只按 MemberId 过滤
        /// </summary>
        PagedInfo<OMSOrderDto> GetMemberOrders(long memberId, MemberOrderQueryDto parm);

        /// <summary>
        /// 会员订单详情（校验归属，不属于该会员返回 null）
        /// </summary>
        OMSOrder GetMemberOrder(long memberId, long id);

        /// <summary>
        /// 会员订单详情 DTO（校验归属，不属于该会员返回 null）。C 端详情接口用这个：
        /// 售后时效布尔在此回填，避免调用方各自取时间 / 配置算出不同结论。
        /// </summary>
        OMSOrderDto GetMemberOrderDetail(long memberId, long id);

        /// <summary>
        /// 会员取消自己的订单（校验归属后复用状态内核的 CancelOrder：仅待付款/待发货可取消并回补库存）
        /// </summary>
        int CancelMemberOrder(long memberId, long id);
        /// <summary>
        /// 会员确认收货（已发货 → 已完成，幂等）
        /// </summary>
        int ConfirmMemberReceipt(long memberId, long id);

        /// <summary>
        /// 下单防刷限流：IP 与手机号双维度配额（10 分钟窗口）。返回 false 时 msg 为提示。
        /// 备注：AspNetCoreRateLimit 已有全局 POST 1次/3s 兜底，此处为业务级配额。
        /// 与验证码相互独立 —— 会员免验证码下单同样要受配额约束，不能一并跳过。
        /// </summary>
        bool TryAcquireCreateQuota(string ip, string phone, out string msg);
    }
}
