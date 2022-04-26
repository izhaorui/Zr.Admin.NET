using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.Service.System.IService
{
    public interface ISysUserService : IBaseService<SysUser>
    {
        public PagedInfo<SysUser> SelectUserList(SysUser user, PagerInfo pager);

        /// <summary>
        /// 通过用户ID查询用户
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public SysUser SelectUserById(long userId);

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
        public long InsertUser(SysUser sysUser);

        /// <summary>
        /// 修改用户信息
        /// </summary>
        /// <param name="sysUser"></param>
        /// <returns></returns>
        public int UpdateUser(SysUser user);

        public int ChangeUser(SysUser user);

        /// <summary>
        /// 重置密码
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public int ResetPwd(long userid, string password);

        public int ChangeUserStatus(SysUser user);

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="user"></param>
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
    }
}
