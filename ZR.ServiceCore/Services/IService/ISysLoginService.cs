using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.ServiceCore.Model.Dto;

namespace ZR.ServiceCore.Services
{
    public interface ISysLoginService : IBaseService<SysLogininfor>
    {
        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="loginBody"></param>
        /// <param name="logininfor"></param>
        /// <returns></returns>
        public SysUser Login(LoginBodyDto loginBody, SysLogininfor logininfor);
        /// <summary>
        /// 手机号登录
        /// </summary>
        /// <param name="loginBody"></param>
        /// <param name="logininfor"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        SysUser PhoneLogin(PhoneLoginDto loginBody, SysLogininfor logininfor, SysUser user);
        /// <summary>
        /// 查询操作日志
        /// </summary>
        PagedInfo<SysLogininfor> GetLoginLog(SysLogininfoQueryDto logininfoDto);
        /// <summary>
        /// 记录登录日志
        /// </summary>
        /// <param name="sysLogininfor"></param>
        /// <returns></returns>
        public void AddLoginInfo(SysLogininfor sysLogininfor);

        /// <summary>
        /// 清空登录日志
        /// </summary>
        public void TruncateLogininfo();

        /// <summary>
        /// 删除登录日志
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public int DeleteLogininforByIds(long[] ids);

        void CheckLockUser(string userName);
        /// <summary>
        /// 查询登录日志统计
        /// </summary>
        /// <returns></returns>
        List<StatiLoginLogDto> GetStatiLoginlog();
    }
}
