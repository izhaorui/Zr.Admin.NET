using Infrastructure;
using Infrastructure.Attribute;
using Microsoft.AspNetCore.Http;
using UAParser;
using ZR.Common;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Repository;
using ZR.ServiceCore.Model.Dto;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 登录
    /// </summary>
    [AppService(ServiceType = typeof(ISysLoginService), ServiceLifetime = LifeTime.Transient)]
    public class SysLoginService : BaseService<SysLogininfor>, ISysLoginService
    {
        private readonly ISysUserService SysUserService;
        private readonly IHttpContextAccessor httpContextAccessor;

        public SysLoginService(ISysUserService sysUserService, IHttpContextAccessor httpContextAccessor)
        {
            SysUserService = sysUserService;
            this.httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// 登录验证
        /// </summary>
        /// <param name="logininfor"></param>
        /// <param name="loginBody"></param>
        /// <returns></returns>
        public SysUser Login(LoginBodyDto loginBody, SysLogininfor logininfor)
        {
            if (loginBody.Password.Length != 32)
            {
                loginBody.Password = NETCore.Encrypt.EncryptProvider.Md5(loginBody.Password);
            }
            SysUser user = SysUserService.Login(loginBody);
            logininfor.UserName = loginBody.Username;
            logininfor.Status = "1";
            logininfor.LoginTime = DateTime.Now;
            logininfor.Ipaddr = loginBody.LoginIP;
            logininfor.ClientId = loginBody.ClientId;

            ClientInfo clientInfo = httpContextAccessor.HttpContext.GetClientInfo();
            logininfor.Browser = clientInfo.ToString();
            logininfor.Os = clientInfo.OS.ToString();

            if (user == null || user.UserId <= 0)
            {
                logininfor.Msg = "用户名或密码错误";
                AddLoginInfo(logininfor);
                throw new CustomException(ResultCode.LOGIN_ERROR, logininfor.Msg, false);
            }
            logininfor.UserId = user.UserId;
            if (user.Status == 1)
            {
                logininfor.Msg = "该用户已禁用";
                AddLoginInfo(logininfor);
                throw new CustomException(ResultCode.LOGIN_ERROR, logininfor.Msg, false);
            }

            logininfor.Status = "0";
            logininfor.Msg = "登录成功";
            AddLoginInfo(logininfor);
            SysUserService.UpdateLoginInfo(loginBody.LoginIP, user.UserId);
            return user;
        }

        /// <summary>
        /// 登录验证
        /// </summary>
        /// <param name="logininfor"></param>
        /// <param name="loginBody"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public SysUser PhoneLogin(PhoneLoginDto loginBody, SysLogininfor logininfor, SysUser user)
        {
            logininfor.UserName = user.UserName;
            logininfor.Status = "1";
            logininfor.LoginTime = DateTime.Now;
            logininfor.Ipaddr = loginBody.LoginIP;

            ClientInfo clientInfo = httpContextAccessor.HttpContext.GetClientInfo();
            logininfor.Browser = clientInfo.ToString();
            logininfor.Os = clientInfo.OS.ToString();

            if (user.Status == 1)
            {
                logininfor.Msg = "该用户已禁用";
                AddLoginInfo(logininfor);
                throw new CustomException(ResultCode.LOGIN_ERROR, logininfor.Msg, false);
            }

            logininfor.Status = "0";
            logininfor.Msg = "登录成功";
            AddLoginInfo(logininfor);
            SysUserService.UpdateLoginInfo(loginBody.LoginIP, user.UserId);
            return user;
        }

        /// <summary>
        /// 查询登录日志
        /// </summary>
        /// <param name="logininfoDto"></param>
        /// <returns></returns>
        public PagedInfo<SysLogininfor> GetLoginLog(SysLogininfoQueryDto logininfoDto)
        {
            var exp = Expressionable.Create<SysLogininfor>();

            exp.AndIF(logininfoDto.BeginTime == null, it => it.LoginTime >= DateTime.Now.ToShortDateString().ParseToDateTime());
            exp.AndIF(logininfoDto.BeginTime != null, it => it.LoginTime >= logininfoDto.BeginTime && it.LoginTime <= logininfoDto.EndTime);
            exp.AndIF(logininfoDto.UserId != null, it => it.UserId == logininfoDto.UserId);
            exp.AndIF(logininfoDto.Status.IfNotEmpty(), f => f.Status == logininfoDto.Status);
            exp.AndIF(logininfoDto.Ipaddr.IfNotEmpty(), f => f.Ipaddr == logininfoDto.Ipaddr);
            exp.AndIF(logininfoDto.UserName.IfNotEmpty(), f => f.UserName.Contains(logininfoDto.UserName));

            var query = Queryable().Where(exp.ToExpression())
            .OrderBy(it => it.InfoId, OrderByType.Desc);

            return query.ToPage(logininfoDto);
        }

        /// <summary>
        /// 记录登录日志
        /// </summary>
        /// <param name="sysLogininfor"></param>
        /// <returns></returns>
        public void AddLoginInfo(SysLogininfor sysLogininfor)
        {
            Insert(sysLogininfor);
        }

        /// <summary>
        /// 清空登录日志
        /// </summary>
        public void TruncateLogininfo()
        {
            Truncate();
        }

        /// <summary>
        /// 删除登录日志
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public int DeleteLogininforByIds(long[] ids)
        {
            return Delete(ids);
        }

        public void CheckLockUser(string userName)
        {
            var lockTimeStamp = CacheService.GetLockUser(userName);
            var lockTime = DateTimeHelper.ToLocalTimeDateBySeconds(lockTimeStamp);
            var ts = lockTime - DateTime.Now;

            if (lockTimeStamp > 0 && ts.TotalSeconds > 0)
            {
                throw new CustomException(ResultCode.LOGIN_ERROR, $"你的账号已被锁,剩余{Math.Round(ts.TotalMinutes, 0)}分钟");
            }
        }

        public List<StatiLoginLogDto> GetStatiLoginlog()
        {
            var time = DateTime.Now;

            //如果是查询当月那么 time就是 DateTime.Now
            var days = (time.AddMonths(1) - time).Days;//获取当月天数
            var dayArray = Enumerable.Range(1, days).Select(it => Convert.ToDateTime(time.ToString("yyyy-MM-" + it))).ToList();//转成时间数组

            var queryableLeft = Context.Reportable(dayArray)
                .ToQueryable<DateTime>();

            var queryableRight = Context.Queryable<SysLogininfor>();
            var list = Context.Queryable(queryableLeft, queryableRight, JoinType.Left, (x1, x2)
                 => x2.LoginTime.ToString("yyyy-MM-dd") == x1.ColumnName.ToString("yyyy-MM-dd"))
                .GroupBy((x1, x2) => x1.ColumnName)
                .Where((x1, x2) => x1.ColumnName >= DateTime.Now.AddDays(-7) && x1.ColumnName <= DateTime.Now)
                .Select((x1, x2) => new StatiLoginLogDto()
                {
                    DeRepeatNum = SqlFunc.AggregateDistinctCount(x2.Ipaddr),
                    Num = SqlFunc.AggregateCount(x2.InfoId),
                    Date = x1.ColumnName,
                })
                .Mapper(it =>
                {
                    it.WeekName = Tools.GetWeekByDate(it.Date);//相当于ToList循环赋值
                }).ToList();
            return list;
        }

    }
}
