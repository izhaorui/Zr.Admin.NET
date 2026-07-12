using Infrastructure;
using Infrastructure.Attribute;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using SqlSugar.IOC;
using UAParser;
using ZR.Common;
using ZR.Infrastructure.Constant;
using ZR.Infrastructure.Helper;
using ZR.Infrastructure.IPTools;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Repository;
using ZR.ServiceCore.Model.Dto;
using ZR.ServiceCore.Resources;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 登录
    /// </summary>
    [AppService(ServiceType = typeof(ISysLoginService), ServiceLifetime = LifeTime.Transient)]
    public class SysLoginService : BaseService<SysLogininfor>, ISysLoginService
    {
        private readonly ISysUserService SysUserService;
        private readonly ISysUserMsgService sysUserMsgService;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public SysLoginService(
            ISysUserService sysUserService, 
            ISysUserMsgService sysUserMsgService,
            IHttpContextAccessor httpContextAccessor,
            IStringLocalizer<SharedResource> localizer)
        {
            SysUserService = sysUserService;
            this.sysUserMsgService = sysUserMsgService;
            this.httpContextAccessor = httpContextAccessor;
            _localizer = localizer;
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
                logininfor.Msg = _localizer["login_pwd_error"].Value;
                AddLoginInfo(logininfor, loginBody.TenantId);
                throw new CustomException(ResultCode.LOGIN_ERROR, logininfor.Msg, false);
            }
            logininfor.UserId = user.UserId;
            if (user.Status == 1)
            {
                logininfor.Msg = _localizer["login_user_disabled"].Value;//該用戶已禁用
                AddLoginInfo(logininfor, loginBody.TenantId);
                throw new CustomException(ResultCode.LOGIN_ERROR, logininfor.Msg, false);
            }

            logininfor.Status = "0";
            logininfor.Msg = "登录成功";
            AddLoginInfo(logininfor, loginBody.TenantId);
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
        public SysUserDto PhoneLogin(PhoneLoginDto loginBody, SysLogininfor logininfor, SysUserDto user)
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
                logininfor.Msg = _localizer["login_user_disabled"].Value;
                AddLoginInfo(logininfor, loginBody.TenantId);
                throw new CustomException(ResultCode.LOGIN_ERROR, logininfor.Msg, false);
            }

            logininfor.Status = "0";
            logininfor.Msg = "登录成功";
            AddLoginInfo(logininfor, loginBody.TenantId);
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
            var db = ResolveTenantDb();
            var exp = Expressionable.Create<SysLogininfor>();

            exp.AndIF(logininfoDto.BeginTime == null, it => it.LoginTime >= DateTime.Now.ToShortDateString().ParseToDateTime());
            exp.AndIF(logininfoDto.BeginTime != null, it => it.LoginTime >= logininfoDto.BeginTime && it.LoginTime <= logininfoDto.EndTime);
            exp.AndIF(logininfoDto.UserId != null, it => it.UserId == logininfoDto.UserId);
            exp.AndIF(logininfoDto.Status.IfNotEmpty(), f => f.Status == logininfoDto.Status);
            exp.AndIF(logininfoDto.Ipaddr.IfNotEmpty(), f => f.Ipaddr == logininfoDto.Ipaddr);
            exp.AndIF(logininfoDto.UserName.IfNotEmpty(), f => f.UserName.Contains(logininfoDto.UserName));

            var query = db.Queryable<SysLogininfor>().Where(exp.ToExpression())
                .OrderBy(it => it.InfoId, OrderByType.Desc);

            var list = query.ToPage(logininfoDto);
            foreach (var item in list.Result)
            {
                if (!HttpContextExtension.HasSensitivePerm(App.HttpContext, SensitivePerms.ViewRealIP))
                {
                    item.Ipaddr = MaskUtil.MaskIp(item.Ipaddr);
                }
            }
            return list;
        }

        /// <summary>
        /// 记录登录日志
        /// </summary>
        /// <param name="sysLogininfor"></param>
        /// <param name="tenantId">租户ID，多租户模式下写入对应租户库</param>
        /// <returns></returns>
        public void AddLoginInfo(SysLogininfor sysLogininfor, string tenantId = null)
        {
            var db = ResolveTenantDb(tenantId);
            db.Insertable(sysLogininfor).ExecuteCommand();
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
            var db = ResolveTenantDb();
            var time = DateTime.Now;

            //如果是查询当月那么 time就是 DateTime.Now
            var days = (time.AddMonths(1) - time).Days;//获取当月天数
            var dayArray = Enumerable.Range(1, days).Select(it => Convert.ToDateTime(time.ToString("yyyy-MM-" + it))).ToList();//转成时间数组

            var queryableLeft = db.Reportable(dayArray)
                .ToQueryable<DateTime>();

            var queryableRight = db.Queryable<SysLogininfor>();
            var list = db.Queryable(queryableLeft, queryableRight, JoinType.Left, (x1, x2)
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

        public string GetAbnormalLoginNotice(SysUser user, string currentLoginIp)
        {
            if (user == null || user.UserId <= 0)
            {
                return string.Empty;
            }

            var currentLocation = NormalizeLoginLocation(GetLocationByIp(currentLoginIp));
            var previousLocation = NormalizeLoginLocation(GetLocationByIp(user.LoginIP));

            if (previousLocation.IsEmpty() || currentLocation.IsEmpty())
            {
                return string.Empty;
            }

            if (string.Equals(previousLocation, currentLocation, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var content = $"检测到您的账号发生异地登录。账号：{user.UserName}；本次地点：{currentLocation}；上次地点：{previousLocation}；登录IP：{currentLoginIp}。如非本人操作，请立即修改密码。";
            sysUserMsgService.AddSysUserMsg(user.UserId, content, UserMsgType.SYSTEM);
            return content;
        }

        private static string GetLocationByIp(string ip)
        {
            if (ip.IsEmpty())
            {
                return string.Empty;
            }

            var ipInfo = IpTool.Search(ip);
            return $"{ipInfo?.Province}-{ipInfo?.City}-{ipInfo?.NetworkOperator}";
        }

        private static string NormalizeLoginLocation(string location)
        {
            if (location.IsEmpty())
            {
                return string.Empty;
            }

            var segments = location
                .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(segment => !segment.Equals("0", StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToArray();

            return segments.Length == 0 ? string.Empty : string.Join("-", segments);
        }

    }
}
