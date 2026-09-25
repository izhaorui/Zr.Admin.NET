using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
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

            var query = db.Queryable<SysLogininfor>().ApplyScope().Where(exp.ToExpression())
                .OrderBy(it => it.InfoId, OrderByType.Desc);

            var list = query.ToPage(logininfoDto);
            list.Result.MaskField(
                HttpContextExtension.HasSensitivePerm(App.HttpContext, SensitivePerms.ViewRealIP),
                it => it.Ipaddr, (it, v) => it.Ipaddr = v, MaskUtil.MaskIp);
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
                // 账号锁定提醒：登录态尚未建立，使用 ClearFilter 跨数据权限解析用户后发送系统消息
                var userId = Context.Queryable<SysUser>().ClearFilter().Where(u => u.UserName == userName).Select(u => u.UserId).First();
                if (userId > 0)
                {
                    var minutes = Math.Round(ts.TotalMinutes, 0);
                    sysUserMsgService.AddSysUserMsg(userId, $"您的账号因多次登录失败已被锁定，剩余{minutes}分钟，解锁后请重试", UserMsgType.SYSTEM);
                }
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

            var queryableRight = db.Queryable<SysLogininfor>().ApplyScope();
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

        /// <summary>
        /// 聚合登录日志安全指标供 AI 解读。走租户库 + 数据范围过滤，与登录日志列表同一口径；
        /// 指标由固定 SQL 聚合，AI 只解读结果，不接触原始日志也不生成任何 SQL。
        /// </summary>
        public LoginSecurityMetricsDto GetLoginSecurityMetrics(LogAiAnalysisInput input)
        {
            var (begin, end) = input.ResolveRange();
            var metrics = new LoginSecurityMetricsDto
            {
                TimeRange = $"{begin:yyyy-MM-dd} ~ {end:yyyy-MM-dd}"
            };

            var db = ResolveTenantDb();

            // 成功/失败总量（sys_logininfor.status：0成功 1失败）
            foreach (var row in BuildRangeQuery(db, begin, end).GroupBy(it => it.Status)
                .Select(it => new { Status = it.Status, Num = SqlFunc.AggregateCount(it.InfoId) })
                .ToList())
            {
                if (row.Status == "0")
                {
                    metrics.SuccessCount = row.Num;
                }
                else if (row.Status == "1")
                {
                    metrics.FailCount = row.Num;
                }
            }
            metrics.TotalCount = metrics.SuccessCount + metrics.FailCount;

            // 每日成功/失败趋势
            metrics.Daily = BuildRangeQuery(db, begin, end).GroupBy(it => it.LoginTime.ToString("yyyy-MM-dd"))
                .Select(it => new
                {
                    Date = it.LoginTime.ToString("yyyy-MM-dd"),
                    Success = SqlFunc.AggregateSum(SqlFunc.IIF(it.Status == "0", 1, 0)),
                    Fail = SqlFunc.AggregateSum(SqlFunc.IIF(it.Status == "1", 1, 0))
                })
                .ToList()
                .OrderBy(x => x.Date)
                .Select(x => new LoginDailyStat { Date = x.Date, Success = x.Success, Fail = x.Fail })
                .ToList();

            // 凌晨 0-6 点活跃（异常时段信号）
            metrics.NightCount = BuildRangeQuery(db, begin, end).Where(it => it.LoginTime.Hour < 6).Count();
            metrics.NightFailCount = BuildRangeQuery(db, begin, end).Where(it => it.LoginTime.Hour < 6 && it.Status == "1").Count();

            // 失败账号 Top：按账号+地点分组后在内存归并，才能拿到地点去重数（异地/共享账号信号）
            var failRows = BuildRangeQuery(db, begin, end).Where(it => it.Status == "1")
                .GroupBy(it => new { it.UserName, it.LoginLocation })
                .Select(it => new
                {
                    it.UserName,
                    it.LoginLocation,
                    Num = SqlFunc.AggregateCount(it.InfoId),
                    Last = SqlFunc.AggregateMax(it.LoginTime)
                })
                .ToList();
            metrics.FailedAccounts = failRows
                .GroupBy(x => x.UserName)
                .Select(g => new LoginFailAccountStat
                {
                    UserName = g.Key,
                    FailCount = g.Sum(x => x.Num),
                    LocationCount = g.Select(x => x.LoginLocation).Where(x => !string.IsNullOrEmpty(x)).Distinct().Count(),
                    Locations = string.Join("、", g.Select(x => x.LoginLocation).Where(x => !string.IsNullOrEmpty(x)).Distinct().Take(5)),
                    LastFailTime = g.Max(x => x.Last)
                })
                .OrderByDescending(x => x.FailCount)
                .Take(10)
                .ToList();

            // 失败 IP Top
            metrics.FailedIps = BuildRangeQuery(db, begin, end).Where(it => it.Status == "1")
                .GroupBy(it => new { it.Ipaddr, it.LoginLocation })
                .Select(it => new
                {
                    it.Ipaddr,
                    it.LoginLocation,
                    Num = SqlFunc.AggregateCount(it.InfoId),
                    Last = SqlFunc.AggregateMax(it.LoginTime)
                })
                .ToList()
                .GroupBy(x => x.Ipaddr)
                .Select(g => new LoginFailIpStat
                {
                    Ipaddr = g.Key,
                    FailCount = g.Sum(x => x.Num),
                    Location = g.Select(x => x.LoginLocation).FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? string.Empty,
                    LastFailTime = g.Max(x => x.Last)
                })
                .OrderByDescending(x => x.FailCount)
                .Take(10)
                .ToList();

            // 客户端环境分布 Top5
            metrics.Browsers = BuildRangeQuery(db, begin, end).GroupBy(it => it.Browser)
                .Select(it => new { Name = it.Browser, Num = SqlFunc.AggregateCount(it.InfoId) })
                .ToList()
                .OrderByDescending(x => x.Num).Take(5)
                .Select(x => new NameCountStat { Name = x.Name, Count = x.Num })
                .ToList();
            metrics.Oses = BuildRangeQuery(db, begin, end).GroupBy(it => it.Os)
                .Select(it => new { Name = it.Os, Num = SqlFunc.AggregateCount(it.InfoId) })
                .ToList()
                .OrderByDescending(x => x.Num).Take(5)
                .Select(x => new NameCountStat { Name = x.Name, Count = x.Num })
                .ToList();

            // IP 面貌：区间去重 IP 与近 30 天未出现的"新 IP"
            var rangeIps = BuildRangeQuery(db, begin, end)
                .Where(it => it.Ipaddr != null && it.Ipaddr != "")
                .Select(it => it.Ipaddr).Distinct().ToList();
            metrics.DistinctIpCount = rangeIps.Count;
            var priorIps = db.Queryable<SysLogininfor>().ApplyScope()
                .Where(it => it.LoginTime >= begin.AddDays(-30) && it.LoginTime < begin && it.Ipaddr != null && it.Ipaddr != "")
                .Select(it => it.Ipaddr).Distinct().ToList()
                .ToHashSet(StringComparer.Ordinal);
            metrics.NewIpCount = rangeIps.Count(x => !priorIps.Contains(x));

            // 与登录日志列表同口径：无"真实 IP 查看"敏感权限时 IP 脱敏后再喂模型/展示，
            // 避免无权限用户绕过列表页拿到明文 IP
            if (!HttpContextExtension.HasSensitivePerm(App.HttpContext, SensitivePerms.ViewRealIP))
            {
                foreach (var ip in metrics.FailedIps)
                {
                    ip.Ipaddr = MaskUtil.MaskIp(ip.Ipaddr);
                }
                metrics.IpMasked = true;
            }

            // 成功侧异地信号：同账号多地点 / 相对近30天新地点 / 2小时内跨地切换
            BuildRemoteLoginMetrics(db, metrics, begin, end, input.Limit);

            return metrics;
        }

        /// <summary>
        /// 成功侧异地信号聚合：同账号成功登录多地点、相对近 30 天成功历史出现新地点、2 小时内跨地点切换。
        /// 地点统一按省-市归一化（忽略运营商与未知段），与登录时的异地提醒同一口径。
        /// limit：返回条数上限（1-50，默认15）。
        /// 为避免大范围全量回表，先 SQL 按 账号+地点 粗聚合，内存归并出候选后再对 Top 账号回表做相邻时序检测。
        /// </summary>
        private static void BuildRemoteLoginMetrics(ISqlSugarClient db, LoginSecurityMetricsDto metrics, DateTime begin, DateTime end, int? limit)
        {
            const int rapidSwitchHours = 2;
            var maxAccounts = Math.Clamp(limit ?? 15, 1, 50);

            // 区间内成功登录（sys_logininfor.status：0成功），按 账号+地点 聚合
            var rows = db.Queryable<SysLogininfor>().ApplyScope()
                .Where(it => it.Status == "0" && it.LoginTime >= begin && it.LoginTime <= end
                    && it.UserName != null && it.UserName != "" && it.LoginLocation != null && it.LoginLocation != "")
                .GroupBy(it => new { it.UserName, it.LoginLocation })
                .Select(it => new
                {
                    it.UserName,
                    it.LoginLocation,
                    Num = SqlFunc.AggregateCount(it.InfoId),
                    Last = SqlFunc.AggregateMax(it.LoginTime)
                })
                .ToList();

            // 近 30 天成功历史地点集合（账号"新地点"判定基准，不含本次区间）
            var histRows = db.Queryable<SysLogininfor>().ApplyScope()
                .Where(it => it.Status == "0" && it.LoginTime >= begin.AddDays(-30) && it.LoginTime < begin
                    && it.UserName != null && it.UserName != "")
                .GroupBy(it => new { it.UserName, it.LoginLocation })
                .Select(it => new { it.UserName, it.LoginLocation })
                .ToList()
                .GroupBy(x => x.UserName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => NormalizeLoginLocation(x.LoginLocation)).Where(x => x.Length > 0)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            // 内存归并：每个账号的地点集合与新增地点
            var candidates = new List<RemoteLoginAccountStat>();
            foreach (var g in rows.GroupBy(x => x.UserName, StringComparer.OrdinalIgnoreCase))
            {
                var locs = g.Select(x => NormalizeLoginLocation(x.LoginLocation))
                    .Where(x => x.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                histRows.TryGetValue(g.Key, out var hist);
                var hasHist = hist != null && hist.Count > 0;
                var newLocs = hasHist
                    ? locs.Where(x => !hist.Contains(x)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
                    : new List<string>();
                if (locs.Count < 2 && newLocs.Count == 0)
                {
                    continue; // 无异地信号：单地点且不是相对历史的新地点
                }

                candidates.Add(new RemoteLoginAccountStat
                {
                    UserName = g.Key,
                    SuccessCount = g.Sum(x => x.Num),
                    LocationCount = locs.Count,
                    Locations = string.Join("、", locs.Take(5)),
                    NewLocationCount = newLocs.Count,
                    NewLocations = string.Join("、", newLocs.Take(3)),
                    LastLoginTime = g.Max(x => x.Last)
                });
            }

            metrics.RemoteLoginAccountCount = candidates.Count;
            if (candidates.Count == 0)
            {
                return;
            }

            // 时序检测只对 Top 账号回表：相邻两次成功登录 2 小时内且地点不同记一次跨地切换
            var top = candidates
                .OrderByDescending(x => x.LocationCount + x.NewLocationCount)
                .ThenByDescending(x => x.LastLoginTime)
                .Take(maxAccounts)
                .ToList();
            var names = top.Select(x => x.UserName).ToList();
            var details = db.Queryable<SysLogininfor>().ApplyScope()
                .Where(it => it.Status == "0" && it.LoginTime >= begin && it.LoginTime <= end
                    && names.Contains(it.UserName) && it.LoginLocation != null && it.LoginLocation != "")
                .OrderBy(it => it.UserName).OrderBy(it => it.LoginTime)
                .Select(it => new { it.UserName, it.LoginTime, it.LoginLocation })
                .ToList();

            foreach (var userGroup in details.GroupBy(x => x.UserName, StringComparer.OrdinalIgnoreCase))
            {
                long switchCount = 0;
                string prevLoc = null;
                DateTime? prevTime = null;
                foreach (var row in userGroup.OrderBy(x => x.LoginTime))
                {
                    var loc = NormalizeLoginLocation(row.LoginLocation);
                    if (prevLoc != null && prevTime.HasValue
                        && !string.Equals(loc, prevLoc, StringComparison.OrdinalIgnoreCase)
                        && (row.LoginTime - prevTime.Value).TotalHours < rapidSwitchHours)
                    {
                        switchCount++;
                    }
                    if (loc.Length > 0)
                    {
                        prevLoc = loc;
                        prevTime = row.LoginTime;
                    }
                    else
                    {
                        prevTime = row.LoginTime; // 地点未知行不改变"已知地点"，仅推进时间
                    }
                }
                top.First(x => string.Equals(x.UserName, userGroup.Key, StringComparison.OrdinalIgnoreCase)).RapidSwitchCount = switchCount;
            }

            metrics.RemoteLoginAccounts = top;
        }

        // 地域分布统计的区间上限：省份数有限，允许比按天趋势更长的跨度
        private const int RegionStatsMaxDays = 180;
        private const string UnknownRegionName = "未知";
        private const string OtherRegionName = "其他";

        /// <summary>
        /// 按省份聚合登录日志的地域分布（供 AI 图表问答使用，不返回原始日志）。
        /// 口径与登录日志列表一致：租户库 + 数据范围过滤（非管理员仅统计本人）。
        /// 地域取自登录时 IP 解析写入的 LoginLocation（格式"省-市-运营商"），取首段作为省份。
        /// </summary>
        public List<LoginRegionStat> GetLoginRegionStats(LogAiAnalysisInput input, int topN = 12)
        {
            input ??= new LogAiAnalysisInput();
            var (begin, end) = input.ResolveRange(RegionStatsMaxDays);
            var db = ResolveTenantDb();

            // 按（账号, 地点）粒度聚合：既避免拉取全量明细，又保证"独立用户数"在同省跨市时不重复计数。
            // 若直接按地点分组求 DistinctCount，同省不同市会把同一账号重复计入。
            var rows = BuildRangeQuery(db, begin, end)
                .GroupBy(it => new { it.UserName, it.LoginLocation })
                .Select(it => new
                {
                    UserName = it.UserName,
                    Location = it.LoginLocation,
                    Num = SqlFunc.AggregateCount(it.InfoId)
                })
                .ToList();

            var loginCount = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var users = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var region = ExtractProvince(row.Location);
                loginCount.TryGetValue(region, out var count);
                loginCount[region] = count + row.Num;

                if (!users.TryGetValue(region, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    users[region] = set;
                }
                // 失败尝试可能没有账号，用空串占位，保证这部分登录次数不丢、且不与其他空值混淆
                set.Add(row.UserName ?? string.Empty);
            }

            var stats = loginCount
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => (
                    Region: kv.Key,
                    LoginCount: kv.Value,
                    Users: users.TryGetValue(kv.Key, out var set) ? set : new HashSet<string>()))
                .ToList();

            return MergeTailRegions(stats, topN);
        }

        /// <summary>
        /// 长尾省份合并为"其他"，避免饼图出现几十个碎扇区。
        /// 合并时独立用户数取并集而非求和，保证口径与未合并时一致。
        /// </summary>
        private static List<LoginRegionStat> MergeTailRegions(
            List<(string Region, long LoginCount, HashSet<string> Users)> stats, int topN)
        {
            if (topN <= 0 || stats.Count <= topN)
            {
                return stats.Select(x => ToRegionStat(x)).ToList();
            }

            var result = stats.Take(topN).Select(ToRegionStat).ToList();
            var tail = stats.Skip(topN).ToList();
            if (tail.Count == 0)
            {
                return result;
            }

            var mergedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in tail)
            {
                mergedUsers.UnionWith(item.Users);
            }

            result.Add(new LoginRegionStat
            {
                Region = OtherRegionName,
                LoginCount = tail.Sum(x => x.LoginCount),
                UserCount = mergedUsers.Count
            });
            return result;
        }

        private static LoginRegionStat ToRegionStat((string Region, long LoginCount, HashSet<string> Users) item)
            => new() { Region = item.Region, LoginCount = item.LoginCount, UserCount = item.Users.Count };

        /// <summary>
        /// 从 LoginLocation（"省-市-运营商"）提取省份。
        /// 无法解析（空值或全为占位 0）时归入"未知"，保持与异地登录检测相同的分段规则。
        /// </summary>
        private static string ExtractProvince(string location)
        {
            if (location.IsEmpty())
            {
                return UnknownRegionName;
            }

            foreach (var segment in location.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                // 与 NormalizeLoginLocation 一致：0 是 IP 库未识别时的占位段
                if (string.Equals(segment, "0", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                return segment;
            }

            return UnknownRegionName;
        }

        /// <summary>
        /// 构建区间内登录日志查询（租户库 + 数据范围过滤）。每次调用返回全新 queryable，
        /// 避免 SqlSugar 同一实例在多次聚合之间复用时共享查询状态产生串扰。
        /// </summary>
        private ISugarQueryable<SysLogininfor> BuildRangeQuery(ISqlSugarClient db, DateTime begin, DateTime end)
        {
            return db.Queryable<SysLogininfor>().ApplyScope()
                .Where(it => it.LoginTime >= begin && it.LoginTime <= end);
        }

        /// <summary>
        /// 检查登录是否发生异地，并生成系统消息提醒用户（仅在成功登录时调用）。
        /// </summary>
        /// <param name="user"></param>
        /// <param name="currentLoginIp"></param>
        /// <returns></returns>
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

            var content = $"⚠️ 账号异地登录提醒\n\n检测到您的账号发生异地登录，请确认是否为本人操作：\n\n账号：{user.UserName}\n本次地点：{currentLocation}\n上次地点：{previousLocation}\n登录 IP：{currentLoginIp}\n\n如非本人操作，请立即修改密码！";
            sysUserMsgService.AddSysUserMsg(user.UserId, content, UserMsgType.SYSTEM);
            return content;
        }

        /// <summary>
        /// 登录时预计算合并后的数据权限等级（取所有角色中最宽松的权限）
        /// </summary>
        public static int ComputeScopeType(List<SysRole> roles)
        {
            if (roles.Any(r => r.RoleKey == GlobalConstant.AdminRole || r.DataScope == (int)DataPermiEnum.All))
                return (int)MergedScopeType.All;
            if (roles.Any(r => r.DataScope == (int)DataPermiEnum.DEPT_CHILD || r.DataScope == (int)DataPermiEnum.CUSTOM))
                return (int)MergedScopeType.DeptList;
            if (roles.Any(r => r.DataScope == (int)DataPermiEnum.DEPT))
                return (int)MergedScopeType.Dept;
            if (roles.Any(r => r.DataScope == (int)DataPermiEnum.SELF))
                return (int)MergedScopeType.Self;
            return (int)MergedScopeType.None;
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
