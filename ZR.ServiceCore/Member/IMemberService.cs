using ZR.Model.Member;
using ZR.Model.Models;

namespace ZR.ServiceCore.Member
{
    /// <summary>
    /// 通用会员 service 接口。
    /// 会员身份为平台级通用能力（与 sys_user 对等、完全隔离），置于框架共享层，
    /// 供商城/内容/社交等业务模块共用，各模块无需引用商城工程。
    /// </summary>
    public interface IMemberService : IBaseService<UmsMember>
    {
        /// <summary>
        /// 发送会员登录验证码（复用短信与缓存设施，落库留痕）
        /// </summary>
        /// <param name="phone">手机号</param>
        /// <param name="ip">请求IP</param>
        SmsCodeLog SendLoginCode(string phone, string ip);

        /// <summary>
        /// 手机号 + 验证码登录；未注册手机号自动注册为新会员
        /// </summary>
        /// <param name="phone">手机号</param>
        /// <param name="code">短信验证码</param>
        /// <param name="ip">请求IP</param>
        /// <returns>登录成功的会员</returns>
        UmsMember LoginByPhone(string phone, string code, string ip);

        /// <summary>
        /// 更新会员资料（昵称/头像，仅更新传入的非空字段）
        /// </summary>
        /// <param name="memberId">会员Id（来自令牌，不可信任前端入参）</param>
        /// <param name="nickName">昵称（null 表示不修改）</param>
        /// <param name="avatar">头像地址（null 表示不修改）</param>
        int UpdateProfile(long memberId, string nickName, string avatar);
    }
}
