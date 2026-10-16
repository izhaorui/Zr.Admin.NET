using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.ServiceCore.Services
{
    public interface ISysUserService : IBaseService<SysUser>
    {
        public PagedInfo<SysUserDto> SelectUserList(SysUserQueryDto user, PagerInfo pager);

        /// <summary>
        /// 通过用户ID查询用户
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public SysUserDto SelectUserById(long userId);

        /// <summary>
        /// 校验用户名称是否唯一
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public string CheckUserNameUnique(string userName);

        /// <summary>
        /// 新增保存用户信息
        /// </summary>
        /// <param name="sysUser"></param>
        /// <returns></returns>
        public SysUser InsertUser(SysUser sysUser);

        /// <summary>
        /// 修改用户信息
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public int UpdateUser(SysUserDto user);

        public int ChangeUser(SysUser user);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="phoneNum"></param>
        /// <returns></returns>
        List<long> CheckPhoneBind(string phoneNum);
        int ChangePhoneNum(long userid, string phoneNum);

        /// <summary>
        /// 重置密码
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="password"></param>
        /// <param name="notifyContent">重置后通知用户的内容，为空则不发送通知</param>
        /// <returns></returns>
        public int ResetPwd(long userid, string password, string notifyContent = null);

        /// <summary>
        /// 是否为初始（默认）密码：开启 sys.account.initPasswordModify 且从未改过密码。
        /// </summary>
        /// <param name="pwdUpdateTime">用户最后密码更新时间，null 表示从未修改</param>
        /// <returns></returns>
        public bool IsInitPassword(DateTime? pwdUpdateTime);

        /// <summary>
        /// 密码是否已过期：按 sys.account.passwordExpireDay 判定，未配置或配置 &lt;=0 时永不过期。
        /// </summary>
        /// <param name="pwdUpdateTime">用户最后密码更新时间，null 视为已过期</param>
        /// <returns></returns>
        public bool IsPasswordExpired(DateTime? pwdUpdateTime);

        public int ChangeUserStatus(SysUser user);

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public int DeleteUser(long userid);

        /// <summary>
        /// 修改用户头像
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public int UpdatePhoto(SysUser user);

        /// <summary>
        /// 注册
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        SysUser Register(RegisterDto dto);
        void CheckUserAllowed(SysUser user);
        void CheckUserDataScope(long userid, long loginUserId);

        /// <summary>
        /// 导入
        /// </summary>
        /// <param name="users"></param>
        /// <returns></returns>
        (string, object, object) ImportUsers(List<SysUser> users);

        SysUser Login(LoginBodyDto user);

        void UpdateLoginInfo(string userIP, long userId);

        /// <summary>
        /// 保存双因子认证状态（绑定/更新密钥与恢复码）。
        /// 密钥须由调用方加密后传入，禁止明文落库。
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="enabled">0未启用 1已启用</param>
        /// <param name="secret">加密后的 TOTP 密钥</param>
        /// <param name="boundAt">绑定时间</param>
        /// <param name="recoveryCodesJson">恢复码哈希 JSON 数组</param>
        int SaveTwoFactor(long userId, int enabled, string secret, DateTime? boundAt, string recoveryCodesJson);

        /// <summary>
        /// 清空双因子认证配置（用户解绑 / 管理员重置）。
        /// 密钥与恢复码一并清空，避免残留可解密的历史密钥。
        /// </summary>
        int ClearTwoFactor(long userId);
    }
}
