using Infrastructure;
using Infrastructure.Attribute;
using System;
using System.Collections.Generic;
using ZR.Common;
using ZR.Model;
using ZR.Model.System.Dto;
using ZR.Model.System;
using ZR.Repository.System;
using ZR.Service.System.IService;

namespace ZR.Service.System
{
    /// <summary>
    /// 登录
    /// </summary>
    [AppService(ServiceType = typeof(ISysLoginService), ServiceLifetime = LifeTime.Transient)]
    public class SysLoginService: BaseService<SysLogininfor>, ISysLoginService
    {
        private SysLogininfoRepository SysLogininfoRepository;

        public SysLoginService(SysLogininfoRepository sysLogininfo)
        {
            SysLogininfoRepository = sysLogininfo;
        }

        /// <summary>
        /// 登录验证
        /// </summary>
        /// <param name="loginBody"></param>
        /// <returns></returns>
        public SysUser Login(LoginBodyDto loginBody, SysLogininfor logininfor)
        {
            //密码md5
            loginBody.Password = NETCore.Encrypt.EncryptProvider.Md5(loginBody.Password);

            SysUser user = SysLogininfoRepository.Login(loginBody);
            logininfor.userName = loginBody.Username;
            logininfor.status = "1";
            logininfor.loginTime = DateTime.Now;

            if (user == null || user.UserId <= 0)
            {
                logininfor.msg = "用户名或密码错误";
                AddLoginInfo(logininfor);
                throw new CustomException(ResultCode.LOGIN_ERROR ,logininfor.msg);
            }
            if (user.Status == "1")
            {
                logininfor.msg = "该用户已禁用";
                AddLoginInfo(logininfor);
                throw new CustomException(ResultCode.LOGIN_ERROR, logininfor.msg);
            }

            logininfor.status = "0";
            logininfor.msg = "登录成功";
            AddLoginInfo(logininfor);
            SysLogininfoRepository.UpdateLoginInfo(loginBody, user.UserId);
            return user;
        }


        /// <summary>
        /// 查询操作日志
        /// </summary>
        /// <param name="logininfoDto"></param>
        /// <param name="pager">分页</param>
        /// <returns></returns>
        public PagedInfo<SysLogininfor> GetLoginLog(SysLogininfor logininfoDto, PagerInfo pager)
        {
            logininfoDto.BeginTime = DateTimeHelper.GetBeginTime(logininfoDto.BeginTime, -1);
            logininfoDto.EndTime = DateTimeHelper.GetBeginTime(logininfoDto.EndTime, 1);

            var list = SysLogininfoRepository.GetLoginLog(logininfoDto, pager);
            return list;
        }

        /// <summary>
        /// 记录登录日志
        /// </summary>
        /// <param name="sysLogininfor"></param>
        /// <returns></returns>
        public void AddLoginInfo(SysLogininfor sysLogininfor)
        {
            SysLogininfoRepository.AddLoginInfo(sysLogininfor);
        }

        /// <summary>
        /// 清空登录日志
        /// </summary>
        public void TruncateLogininfo()
        {
            SysLogininfoRepository.TruncateLogininfo();
        }

        /// <summary>
        /// 删除登录日志
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public int DeleteLogininforByIds(long[] ids)
        {
            return SysLogininfoRepository.DeleteLogininforByIds(ids);
        }
    }
}
