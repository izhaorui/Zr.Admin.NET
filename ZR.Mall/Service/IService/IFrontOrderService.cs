using ZR.Mall.Model;
using ZR.Mall.Model.Dto;

namespace ZR.Mall.Service.IService
{
    /// <summary>
    /// C 端（游客 / 会员）订单交易：下单、支付入口、我的订单、会员取消。
    ///
    /// 与后台运营（IOMSOrderService）、状态内核（IOMSOrderStateService）分离。
    /// 本接口的方法**一律涉及订单归属校验**（memberId / 手机号），是"用户侧"信任模型的入口；
    /// 后台方法则依赖权限过滤器，两者不可混用。
    /// </summary>
    public interface IFrontOrderService : IBaseService<OMSOrder>
    {
        /// <summary>
        /// 游客/C 端下单：校验库存、限购、计算金额、写订单与订单项、原子扣库存并累加销量。
        /// memberId 非空时把订单绑定到该会员（"我的订单"据此归属），为 null 时为游客匿名下单。
        /// 幂等：同一 RequestId（或相同手机号+订单项签名）短窗口内只创建一次订单。
        /// </summary>
        OMSOrder CreateOrder(CreateOrderDto dto, long? memberId = null);

        /// <summary>
        /// 模拟支付（游客直接支付）：校验订单号+手机号匹配后完成支付（待付款 → 待发货）。
        /// </summary>
        OMSOrder PayOrder(string orderNo, string phone);

        /// <summary>
        /// 会员"我的订单"分页：会员本人下单(MemberId) + 同手机号的历史游客订单(GuestPhone) 合并
        /// </summary>
        PagedInfo<OMSOrderDto> GetMemberOrders(long memberId, string phone, MemberOrderQueryDto parm);

        /// <summary>
        /// 会员订单详情（校验归属，不属于该会员返回 null）
        /// </summary>
        OMSOrder GetMemberOrder(long memberId, string phone, long id);

        /// <summary>
        /// 会员取消自己的订单（校验归属后复用状态内核的 CancelOrder：仅待付款/待发货可取消并回补库存）
        /// </summary>
        int CancelMemberOrder(long memberId, string phone, long id);

        /// <summary>
        /// 下单防刷限流：IP 与手机号双维度配额（10 分钟窗口）。返回 false 时 msg 为提示。
        /// 备注：AspNetCoreRateLimit 已有全局 POST 1次/3s 兜底，此处为业务级配额。
        /// 与验证码相互独立 —— 会员免验证码下单同样要受配额约束，不能一并跳过。
        /// </summary>
        bool TryAcquireCreateQuota(string ip, string phone, out string msg);
    }
}
