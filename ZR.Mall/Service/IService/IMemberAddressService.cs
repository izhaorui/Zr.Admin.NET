using ZR.Mall.Model;
using ZR.Mall.Model.Dto;

namespace ZR.Mall.Service.IService
{
    /// <summary>
    /// 会员收货地址 service 接口。
    /// 所有方法均以 memberId（来自会员令牌）为归属过滤条件，禁止跨会员访问。
    /// </summary>
    public interface IMemberAddressService : IBaseService<MMSUserAddress>
    {
        /// <summary>
        /// 会员地址列表（默认地址排最前）
        /// </summary>
        List<MMSUserAddress> GetList(long memberId);

        /// <summary>
        /// 新增地址；该会员首条地址自动设为默认
        /// </summary>
        MMSUserAddress Add(long memberId, MemberAddressDto dto);

        /// <summary>
        /// 修改地址（仅能改自己的地址）
        /// </summary>
        int UpdateAddress(long memberId, MemberAddressDto dto);

        /// <summary>
        /// 删除地址（删默认地址后自动把最新一条设为默认）
        /// </summary>
        int DeleteAddress(long memberId, long id);

        /// <summary>
        /// 设为默认地址（事务内保证同会员仅一条默认）
        /// </summary>
        int SetDefault(long memberId, long id);
    }
}
