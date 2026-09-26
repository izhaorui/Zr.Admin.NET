using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Model;
using Microsoft.AspNetCore.SignalR;
using MiniExcelLibs;
using System.Text;
using ZR.Common;
using ZR.Model;
using ZR.Model.Content;
using ZR.Model.Models;
using ZR.Model.social;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Model.System.Enums;
using ZR.Model.System.Tenant;
using ZR.ServiceCore.Sms;
using ZR.ServiceCore.Signalr;
using ZR.ServiceCore.SqlSugar;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 租户服务
    /// </summary>
    [AppService(ServiceType = typeof(ISysTenantService), ServiceLifetime = LifeTime.Transient)]
    public class SysTenantService : BaseService<SysTenant>, ISysTenantService
    {
        private readonly IEnumerable<ITenantModuleInitializer> _moduleInitializers;
        private readonly ISysUserMsgService _sysUserMsgService;
        private readonly IHubContext<MessageHub> _hubContext;
        private readonly ISmsSender _smsSender;

        public SysTenantService(
            IEnumerable<ITenantModuleInitializer> moduleInitializers,
            ISysUserMsgService sysUserMsgService,
            IHubContext<MessageHub> hubContext,
            ISmsSender smsSender)
        {
            _moduleInitializers = moduleInitializers ?? Enumerable.Empty<ITenantModuleInitializer>();
            _sysUserMsgService = sysUserMsgService;
            _hubContext = hubContext;
            _smsSender = smsSender;
        }

        /// <summary>
        /// 向租户管理员推送系统消息。租户管理员约定为各租户库内 UserId=1 的管理员用户（租户权限初始化时建立）。
        /// 显式传入 tenantId，确保后台定时任务（租户上下文兜底为主库）也能正确归属消息。
        /// </summary>
        private void SendTenantAdminMessage(string tenantId, string content)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) return;
            _sysUserMsgService.AddSysUserMsg(1, content, UserMsgType.TENANT_NOTICE, tenantId);
        }

        /// <summary>
        /// 向租户联系人发送短信通知（依赖 ISmsSender 抽象，不绑定具体服务商）。
        /// 模板编码取自 SmsOptions.Templates[templateKey]；未配置模板或联系人手机号为空时静默跳过。
        /// 短信服务未启用时由 DefaultSmsSender 走模拟发送（仅记日志）。发送失败仅记日志，不影响主流程。
        /// 内置场景 key：tenantExpireRemind / tenantSuspended / tenantDecommissioned / tenantPlanDegraded
        /// </summary>
        /// <param name="phone">联系人手机号</param>
        /// <param name="templateKey">SmsOptions.Templates 中的场景 key</param>
        /// <param name="templateParams">模板参数（如 days/expiryDate/date）</param>
        private void SendTenantSms(string phone, string templateKey, Dictionary<string, string> templateParams)
        {
            if (string.IsNullOrWhiteSpace(phone)) return;

            try
            {
                var templateCode = App.OptionsSetting?.SmsOptions?.Templates?.GetValueOrDefault(templateKey);
                if (string.IsNullOrWhiteSpace(templateCode))
                {
                    Log.WriteLine(ConsoleColor.DarkGray, $"[SysTenantService] 短信模板[{templateKey}]未配置(SmsOptions.Templates)，跳过短信通知");
                    return;
                }

                var result = _smsSender.Send(new SmsMessage
                {
                    PhoneNum = phone.Trim(),
                    TemplateCode = templateCode,
                    TemplateParams = templateParams,
                    SendType = 6
                });

                if (!result.Success)
                {
                    Log.WriteLine(ConsoleColor.Yellow, $"[SysTenantService] 租户短信发送失败({templateKey})：{result.ErrorCode} {result.ErrorMsg}");
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(ConsoleColor.Red, $"[SysTenantService] 租户短信发送异常({templateKey})：{ex.Message}");
            }
        }

        public PagedInfo<SysTenant> GetPageList(SysTenantQueryDto parm)
        {
            var predicate = Expressionable.Create<SysTenant>();
            predicate = predicate.And(x => x.DelFlag == DelFlagEnum.Normal);
            predicate = predicate.AndIF(!string.IsNullOrWhiteSpace(parm.TenantId), x => x.TenantId.Contains(parm.TenantId));
            predicate = predicate.AndIF(!string.IsNullOrWhiteSpace(parm.TenantName), x => x.TenantName.Contains(parm.TenantName));
            predicate = predicate.AndIF(!string.IsNullOrWhiteSpace(parm.Domain), x => x.Domain.Contains(parm.Domain));
            predicate = predicate.AndIF(parm.Status != null, x => x.Status == parm.Status);
            predicate = predicate.AndIF(parm.BeginTime != null, x => x.Create_time >= parm.BeginTime);
            predicate = predicate.AndIF(parm.EndTime != null, x => x.Create_time <= parm.EndTime);

            return GetPages(predicate.ToExpression(), parm);
        }

        public string CheckTenantIdUnique(SysTenant tenant)
        {
            var tenantId = tenant?.Id ?? 0;
            var info = Queryable().First(x => x.TenantId == tenant.TenantId && x.DelFlag == DelFlagEnum.Normal);
            if (info != null && info.Id != tenantId)
            {
                return UserConstants.NOT_UNIQUE;
            }
            return UserConstants.UNIQUE;
        }

        public SysTenant GetByTenantId(string tenantId)
        {
            return Queryable().First(x => x.TenantId == tenantId && x.DelFlag == DelFlagEnum.Normal);
        }

        private static readonly string TenantDomainMapCacheKey = "TENANT_DOMAIN_MAP";

        /// <summary>
        /// 获取域名→租户ID映射（平台级缓存，约 10 分钟）。键为租户 Domain 字段（小写），值为 TenantId。
        /// 仅包含正常状态(Status=0)且已绑定域名的租户。供中间件按访问域名解析租户。
        /// </summary>
        public Dictionary<string, string> GetDomainTenantMap()
        {
            var map = CacheHelper.GetCache<Dictionary<string, string>>(TenantDomainMapCacheKey);
            if (map != null)
            {
                return map;
            }

            var list = Queryable().Where(x => x.DelFlag == DelFlagEnum.Normal && x.Status == 0).ToList();
            map = list
                .Where(x => !string.IsNullOrWhiteSpace(x.Domain))
                .ToDictionary(x => x.Domain.Trim().ToLowerInvariant(), x => x.TenantId, StringComparer.OrdinalIgnoreCase);

            CacheHelper.SetCache(TenantDomainMapCacheKey, map, 10);
            return map;
        }

        /// <summary>
        /// 清除域名→租户ID映射缓存。租户增改/停服/注销后调用，使下次请求即时重新加载。
        /// </summary>
        public void RemoveDomainMapCache()
        {
            CacheHelper.Remove(TenantDomainMapCacheKey);
        }

        /// <summary>
        /// 校验域名(Domain)全局唯一。空域名视为允许（不绑定）。
        /// </summary>
        public string CheckDomainUnique(string domain, long excludeId)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                return UserConstants.UNIQUE;
            }

            var info = Queryable().First(x => x.Domain == domain.Trim() && x.DelFlag == DelFlagEnum.Normal);
            if (info != null && info.Id != excludeId)
            {
                return UserConstants.NOT_UNIQUE;
            }

            return UserConstants.UNIQUE;
        }

        public void CheckTenant(string tenantId)
        {
            if (!App.IsTenantEnabled())
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new CustomException(ResultCode.LOGIN_ERROR, "租户标识不能为空", false);
            }

            var tenant = GetByTenantId(tenantId);
            if (tenant == null)
            {
                throw new CustomException(ResultCode.LOGIN_ERROR, "租户不存在", false);
            }

            if (tenant.Status == 1)
            {
                throw new CustomException(ResultCode.LOGIN_ERROR, "租户已停用", false);
            }

            if (tenant.ExpireTime.HasValue && tenant.ExpireTime.Value < DateTime.Now)
            {
                throw new CustomException(ResultCode.LOGIN_ERROR, "租户已过期", false);
            }
        }

        public TenantLifecycleResult ProvisionTenant(TenantProvisionDto dto, string operatorName)
        {
            var result = CreateResult(dto?.TenantId, "provision");
            EnsureTenantFeatureEnabled();
            EnsureDefaultPlans();

            if (dto == null)
            {
                throw new CustomException("请求参数错误");
            }

            if (string.IsNullOrWhiteSpace(dto.TenantId))
            {
                throw new CustomException("租户标识不能为空");
            }

            var tenantId = dto.TenantId.Trim();
            result.TenantId = tenantId;
            AppendStep(result, "validate", true, "参数校验通过");

            // 域名绑定：留空默认等于租户标识（即开即用）；提供则校验全局唯一
            var domain = string.IsNullOrWhiteSpace(dto.Domain) ? tenantId : dto.Domain.Trim();
            if (UserConstants.NOT_UNIQUE.Equals(CheckDomainUnique(domain, 0)))
            {
                throw new CustomException($"域名绑定[{domain}]已被其他租户占用");
            }

            EnsureDbConfigExists(tenantId);
            AppendStep(result, "ensure-db-config", true, $"检测到租户数据库配置[{tenantId}]");

            if (GetByTenantId(tenantId) != null)
            {
                throw new CustomException($"租户[{tenantId}]已存在");
            }

            var tenant = new SysTenant
            {
                TenantId = tenantId,
                TenantName = dto.TenantName,
                CompanyName = dto.CompanyName,
                ContactName = dto.ContactName,
                ContactPhone = dto.ContactPhone,
                Domain = domain,
                Logo = dto.Logo,
                Status = dto.EnableAfterInit ? 0 : 1,
                ExpireTime = dto.ExpireTime,
                DelFlag = DelFlagEnum.Normal,
                Remark = dto.Remark,
                Create_by = operatorName,
                Create_time = DateTime.Now
            };

            Insert(tenant);
            RemoveDomainMapCache();
            AppendStep(result, "create-tenant", true, "租户记录创建成功");

            if (dto.InitializeNow)
            {
                var initResult = InitializeTenant(new TenantInitializeDto
                {
                    TenantId = tenantId,
                    CreateDatabaseIfNotExists = true,
                    SeedFromMain = dto.SeedFromMain,
                    EnsureMainTenantRecord = false
                });

                result.Steps.AddRange(initResult.Steps);
            }

            // 套餐分配：TrialDays>0 时开通即试用 Pro（到期后自动回落默认套餐），否则按默认套餐
            var planCode = GetDefaultPlanCode();
            var planEndTime = dto.ExpireTime;
            var planRemark = "开通时自动分配默认套餐";
            if (dto.TrialDays is > 0)
            {
                if (dto.TrialDays > 365)
                {
                    throw new CustomException("试用期天数不能超过365天");
                }

                var trialPlan = GetPlanByCode("pro");
                if (trialPlan != null && trialPlan.Status == 0)
                {
                    planCode = "pro";
                    planRemark = $"开通试用Pro套餐{dto.TrialDays}天，期满自动回落默认套餐";
                }
                else
                {
                    planRemark = $"未配置可用Pro套餐，试用{dto.TrialDays}天改为默认套餐";
                }
                planEndTime = DateTime.Now.AddDays(dto.TrialDays.Value);
            }

            AssignTenantPlan(new TenantPlanAssignDto
            {
                TenantId = tenantId,
                PlanCode = planCode,
                StartTime = DateTime.Now,
                EndTime = planEndTime,
                Remark = planRemark
            }, operatorName);
            AppendStep(result, "assign-plan", true,
                dto.TrialDays is > 0 ? $"已分配套餐[{planCode}]，试用期至{planEndTime:yyyy-MM-dd}" : "已分配默认套餐free");

            result.Success = true;
            result.Message = "租户开通完成";
            return result;
        }

        /// <summary>
        /// 初始化租户数据库和基础数据
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        /// <exception cref="CustomException"></exception>
        public TenantLifecycleResult InitializeTenant(TenantInitializeDto dto)
        {
            EnsureTenantFeatureEnabled();

            if (dto == null || string.IsNullOrWhiteSpace(dto.TenantId))
            {
                throw new CustomException("租户标识不能为空");
            }

            var tenantId = dto.TenantId.Trim();
            var result = CreateResult(tenantId, "initialize");

            EnsureDbConfigExists(tenantId);
            AppendStep(result, "ensure-db-config", true, $"检测到租户数据库配置[{tenantId}]");
            
            var db = ResolveTenantDb(tenantId);
            if (dto.CreateDatabaseIfNotExists)
            {
                db.DbMaintenance.CreateDatabase();
                AppendStep(result, "create-db", true, "数据库创建/校验成功");
            }

            // 初始化当前租户业务常用表（建表 + 存量补列），与启动补齐共用 TenantBusinessEntityTypes。
            DbMigrationService.EnsureEntitySchemas(db, DbMigrationService.TenantBusinessEntityTypes);

            // 调用各业务模块的租户级表初始化器（如商城、工作流），由模块自己决定需要创建哪些表
            foreach (var initializer in _moduleInitializers)
            {
                var summary = initializer.InitializeTenant(tenantId);
                AppendStep(result, $"init-module-{initializer.ModuleName.ToLowerInvariant()}", true, summary);
            }

            AppendStep(result, "init-schema", true, "租户基础表初始化完成");

            if (dto.SeedFromMain)
            {
                var seedSummary = SeedTenantBaseDataFromMainDb(tenantId);
                AppendStep(result, "seed-data", true, seedSummary);
            }

            var permissionSummary = SeedTenantPermissionDataFromMainDb(tenantId);
            AppendStep(result, "init-permission-data", true, permissionSummary);

            if (dto.EnsureMainTenantRecord)
            {
                var tenant = GetByTenantId(tenantId);
                if (tenant == null)
                {
                    Insert(new SysTenant
                    {
                        TenantId = tenantId,
                        TenantName = tenantId,
                        Status = 0,
                        DelFlag = DelFlagEnum.Normal,
                        Remark = "初始化流程自动创建",
                        Create_time = DateTime.Now
                    });
                    AppendStep(result, "ensure-tenant-record", true, "主库租户记录已自动创建");
                }
                else
                {
                    AppendStep(result, "ensure-tenant-record", true, "主库租户记录已存在");
                }
            }

            result.Success = true;
            result.Message = "租户初始化完成";
            return result;
        }

        public TenantLifecycleResult SuspendTenant(string tenantId, string operatorName, string remark = null)
        {
            EnsureTenantFeatureEnabled();

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new CustomException("租户标识不能为空");
            }

            var normalizedTenantId = tenantId.Trim();
            var result = CreateResult(normalizedTenantId, "suspend");
            var mainDb = App.MainDbConfigId;
            if (string.Equals(normalizedTenantId, mainDb, StringComparison.OrdinalIgnoreCase))
            {
                throw new CustomException("默认租户不允许停服");
            }

            var tenant = GetByTenantId(normalizedTenantId);
            if (tenant == null)
            {
                throw new CustomException("租户不存在");
            }

            tenant.Status = 1;
            tenant.Remark = MergeRemark(tenant.Remark, remark, "停服");
            tenant.Update_by = operatorName;
            tenant.Update_time = DateTime.Now;
            Update(tenant, it => new { it.Status, it.Remark, it.Update_by, it.Update_time });
            RemoveDomainMapCache();

            var suspendReason = string.IsNullOrEmpty(remark) ? "" : $"，原因：{remark}";
            SendTenantAdminMessage(normalizedTenantId, $"您的租户已暂停服务{suspendReason}，如有疑问请联系平台管理员。");
            SendTenantSms(tenant.ContactPhone, "tenantSuspended", new Dictionary<string, string>
            {
                ["date"] = DateTime.Now.ToString("yyyy-MM-dd"),
                ["reason"] = string.IsNullOrEmpty(remark) ? "服务管理" : remark
            });

            var kicked = KickTenantOnlineUsers(normalizedTenantId, $"您的租户已暂停服务{suspendReason}，请重新登录或联系平台管理员");
            AppendStep(result, "kick-online-users", true, $"已通知{kicked}个在线连接强制下线");
            AppendStep(result, "disable-login", true, "租户状态已切换为停用");
            result.Success = true;
            result.Message = "租户停服完成";
            return result;
        }

        /// <summary>
        /// 租户续费
        /// </summary>
        /// <param name="dto"></param>
        /// <param name="operatorName"></param>
        /// <returns></returns>
        /// <exception cref="CustomException"></exception>
        public TenantLifecycleResult RenewTenant(TenantRenewDto dto, string operatorName)
        {
            EnsureTenantFeatureEnabled();

            if (dto == null || string.IsNullOrWhiteSpace(dto.TenantId))
            {
                throw new CustomException("租户标识不能为空");
            }

            var tenantId = dto.TenantId.Trim();
            var result = CreateResult(tenantId, "renew");
            var tenant = GetByTenantId(tenantId);
            if (tenant == null)
            {
                throw new CustomException("租户不存在");
            }

            var renewFrom = tenant.ExpireTime.HasValue && tenant.ExpireTime.Value > DateTime.Now
                ? tenant.ExpireTime.Value
                : DateTime.Now;

            DateTime? newExpireTime = dto.NewExpireTime;
            if (!newExpireTime.HasValue && dto.ExtendDays.HasValue)
            {
                newExpireTime = renewFrom.AddDays(dto.ExtendDays.Value);
            }

            if (!newExpireTime.HasValue)
            {
                throw new CustomException("请提供新到期时间或续费天数");
            }

            tenant.ExpireTime = newExpireTime;
            tenant.Status = 0;
            tenant.Remark = MergeRemark(tenant.Remark, dto.Remark, "续费");
            tenant.Update_by = operatorName;
            tenant.Update_time = DateTime.Now;
            Update(tenant, it => new { it.ExpireTime, it.Status, it.Remark, it.Update_by, it.Update_time });

            // 同步当前生效的套餐绑定有效期：绑定EndTime原随开通时的ExpireTime写入，
            // 若不联动会在旧时间点失配，导致 ResolveActiveTenantPlanBinding 匹配失败静默回落默认套餐。
            var bindingRows = Context.Updateable<SysTenantPlanBinding>()
                .SetColumns(x => new SysTenantPlanBinding
                {
                    EndTime = newExpireTime,
                    Update_by = operatorName,
                    Update_time = DateTime.Now
                })
                .Where(x => x.TenantId == tenantId && x.DelFlag == DelFlagEnum.Normal && x.Status == 0)
                .ExecuteCommand();
            if (bindingRows > 0)
            {
                AppendStep(result, "sync-plan-binding", true, $"已同步{bindingRows}条套餐绑定有效期至{newExpireTime:yyyy-MM-dd HH:mm:ss}");
            }

            var currentPlan = GetCurrentTenantPlan(tenantId);
            InsertTenantOrder(tenantId, "renew", currentPlan?.PlanCode,
                DateTime.Now, newExpireTime, dto.Amount, operatorName,
                dto.ExtendDays.HasValue ? $"续费{dto.ExtendDays}天" : $"续费至{newExpireTime:yyyy-MM-dd}");

            SendTenantAdminMessage(tenantId, $"您的租户已续费成功，服务已恢复，到期时间更新为{newExpireTime:yyyy-MM-dd}。");

            AppendStep(result, "extend-expire-time", true, $"租户到期时间更新为{newExpireTime:yyyy-MM-dd HH:mm:ss}");
            AppendStep(result, "enable-tenant", true, "租户状态已恢复为可用");
            result.Success = true;
            result.Message = "租户续费完成";
            return result;
        }

        /// <summary>
        /// 删除租户（注销）操作，支持逻辑删除或保留记录
        /// </summary>
        /// <param name="dto"></param>
        /// <param name="operatorName"></param>
        /// <returns></returns>
        /// <exception cref="CustomException"></exception>
        public TenantLifecycleResult DecommissionTenant(TenantDecommissionDto dto, string operatorName)
        {
            EnsureTenantFeatureEnabled();

            if (dto == null || string.IsNullOrWhiteSpace(dto.TenantId))
            {
                throw new CustomException("租户标识不能为空");
            }

            var tenantId = dto.TenantId.Trim();
            var result = CreateResult(tenantId, "decommission");
            var mainDb = App.MainDbConfigId;
            if (string.Equals(tenantId, mainDb, StringComparison.OrdinalIgnoreCase))
            {
                throw new CustomException("默认租户不允许删除");
            }

            var tenant = GetByTenantId(tenantId);
            if (tenant == null)
            {
                throw new CustomException("租户不存在");
            }

            tenant.Status = 1;
            tenant.Remark = MergeRemark(tenant.Remark, dto.Remark, "注销");
            tenant.Update_by = operatorName;
            tenant.Update_time = DateTime.Now;

            if (dto.DeleteRecord)
            {
                tenant.DelFlag = DelFlagEnum.Deleted;
                Update(tenant, it => new { it.Status, it.DelFlag, it.Remark, it.Update_by, it.Update_time });
                AppendStep(result, "delete-record", true, "租户记录已逻辑删除");
            }
            else
            {
                Update(tenant, it => new { it.Status, it.Remark, it.Update_by, it.Update_time });
                AppendStep(result, "disable-tenant", true, "租户已停服（保留记录）");
            }

            var kicked = KickTenantOnlineUsers(tenantId, "您的租户已注销下线，如有疑问请联系平台管理员");
            AppendStep(result, "kick-online-users", true, $"已通知{kicked}个在线连接强制下线");

            SendTenantSms(tenant.ContactPhone, "tenantDecommissioned", new Dictionary<string, string>
            {
                ["date"] = DateTime.Now.ToString("yyyy-MM-dd")
            });

            RemoveDomainMapCache();
            result.Success = true;
            result.Message = "租户删除完成";
            return result;
        }

        /// <summary>
        /// 踢出指定租户全部在线用户：按 MessageHub.OnlineClients 过滤该租户连接并发送强退通知。
        /// 推送失败仅记日志，不影响停服/注销主流程。
        /// </summary>
        public int KickTenantOnlineUsers(string tenantId, string reason)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) return 0;

            try
            {
                var connIds = MessageHub.OnlineClients.Values
                    .Where(u => string.Equals(u.TenantId, tenantId.Trim(), StringComparison.OrdinalIgnoreCase))
                    .Select(u => u.ConnnectionId)
                    .ToList();

                if (connIds.Count == 0) return 0;

                _hubContext.Clients.Clients(connIds)
                    .SendAsync(HubsConstant.ForceUser, new { Reason = reason })
                    .GetAwaiter().GetResult();

                Log.WriteLine(ConsoleColor.Yellow, $"[SysTenantService] 已向租户[{tenantId}]推送强退通知，连接数: {connIds.Count}");
                return connIds.Count;
            }
            catch (Exception ex)
            {
                Log.WriteLine(ConsoleColor.Red, $"[SysTenantService] 租户[{tenantId}]在线用户强退通知失败: {ex.Message}");
                return 0;
            }
        }

        public List<TenantPlanDto> GetTenantPlanList()
        {
            EnsureDefaultPlans();

            var plans = Context.Queryable<SysTenantPlan>()
                .Where(x => x.DelFlag == DelFlagEnum.Normal)
                .OrderBy(x => x.Sort)
                .OrderBy(x => x.Id)
                .ToList();

            var menuCounts = Context.Queryable<SysTenantPlanMenu>()
                .Where(x => plans.Select(p => p.PlanCode).Contains(x.PlanCode))
                .GroupBy(x => x.PlanCode)
                .Select(x => new { PlanCode = x.PlanCode, Count = SqlFunc.AggregateCount(x.MenuId) })
                .ToList()
                .ToDictionary(x => x.PlanCode, x => x.Count, StringComparer.OrdinalIgnoreCase);

            return plans.Select(x => new TenantPlanDto
            {
                Id = x.Id,
                PlanCode = x.PlanCode,
                PlanName = x.PlanName,
                MaxUsers = x.MaxUsers,
                Status = x.Status,
                IsDefault = x.IsDefault,
                Sort = x.Sort,
                MenuCount = menuCounts.GetValueOrDefault(x.PlanCode)
            }).ToList();
        }

        public SysTenantPlan GetPlanById(long id)
        {
            return Context.Queryable<SysTenantPlan>()
                .Where(x => x.Id == id && x.DelFlag == DelFlagEnum.Normal)
                .First();
        }

        public SysTenantPlan GetPlanByCode(string planCode)
        {
            return Context.Queryable<SysTenantPlan>()
                .Where(x => x.PlanCode == planCode && x.DelFlag == DelFlagEnum.Normal)
                .First();
        }

        public long InsertPlan(SysTenantPlan plan)
        {
            var exists = Context.Queryable<SysTenantPlan>()
                .Any(x => x.PlanCode == plan.PlanCode && x.DelFlag == DelFlagEnum.Normal);
            if (exists)
                throw new CustomException($"套餐编码[{plan.PlanCode}]已存在");

            plan.Create_time = DateTime.Now;
            return Context.Insertable(plan).ExecuteReturnBigIdentity();
        }

        public int UpdatePlan(SysTenantPlan plan)
        {
            if (plan == null || plan.Id <= 0)
                throw new CustomException("无效的套餐ID");

            var existing = GetPlanById(plan.Id);
            if (existing == null)
                throw new CustomException("套餐不存在");

            plan.Update_time = DateTime.Now;
            return Context.Updateable(plan)
                .IgnoreColumns(x => new { x.Create_time, x.DelFlag })
                .ExecuteCommand();
        }

        public int DeletePlan(long id)
        {
            if (id <= 0)
                throw new CustomException("无效的套餐ID");

            var plan = GetPlanById(id);
            if (plan == null)
                throw new CustomException("套餐不存在");

            var hasBinding = Context.Queryable<SysTenantPlanBinding>()
                .Any(x => x.PlanCode == plan.PlanCode && x.DelFlag == DelFlagEnum.Normal && x.Status == 0);
            if (hasBinding)
                throw new CustomException($"套餐[{plan.PlanName}]下仍有租户绑定，不可删除");

            Context.Updateable<SysTenantPlan>()
                .SetColumns(x => new SysTenantPlan { DelFlag = DelFlagEnum.Deleted, Update_time = DateTime.Now })
                .Where(x => x.Id == id)
                .ExecuteCommand();

            Context.Deleteable<SysTenantPlanMenu>()
                .Where(x => x.PlanCode == plan.PlanCode)
                .ExecuteCommand();

            ClearTenantPlanPermsCache(plan.PlanCode);

            return 1;
        }

        public TenantCurrentPlanDto GetCurrentTenantPlan(string tenantId)
        {
            EnsureDefaultPlans();

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                tenantId = App.GetCurrentTenantId();
            }
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new CustomException("租户标识不能为空");
            }

            var binding = ResolveActiveTenantPlanBinding(tenantId.Trim());
            var plan = ResolvePlanByBindingOrDefault(binding);

            if (plan == null)
            {
                return null;
            }

            var maxUsers = binding?.MaxUsersOverride ?? plan.MaxUsers;

            var currentUsers = 0;
            var dbConfigExists = App.OptionsSetting?.DbConfigs?.Any(x => string.Equals(x.ConfigId, tenantId, StringComparison.OrdinalIgnoreCase)) == true;
            if (dbConfigExists)
            {
                var tenantDb = ResolveTenantDb(tenantId);
                currentUsers = tenantDb.Queryable<SysUser>().Count(x => x.DelFlag == DelFlagEnum.Normal);
            }

            return new TenantCurrentPlanDto
            {
                TenantId = tenantId,
                PlanCode = plan.PlanCode,
                PlanName = plan.PlanName,
                MaxUsers = maxUsers,
                CurrentUsers = currentUsers,
                StartTime = binding?.StartTime,
                EndTime = binding?.EndTime,
                IsExpired = binding?.EndTime.HasValue == true && binding.EndTime.Value < DateTime.Now
            };
        }

        public TenantCurrentPlanDto AssignTenantPlan(TenantPlanAssignDto dto, string operatorName)
        {
            EnsureTenantFeatureEnabled();
            EnsureDefaultPlans();

            if (dto == null || string.IsNullOrWhiteSpace(dto.TenantId) || string.IsNullOrWhiteSpace(dto.PlanCode))
            {
                throw new CustomException("租户标识与套餐编码不能为空");
            }

            var tenantId = dto.TenantId.Trim();
            var planCode = dto.PlanCode.Trim();

            var tenant = GetByTenantId(tenantId);
            if (tenant == null)
            {
                throw new CustomException("租户不存在");
            }

            var plan = Context.Queryable<SysTenantPlan>().First(x => x.PlanCode == planCode && x.DelFlag == DelFlagEnum.Normal && x.Status == 0);
            if (plan == null)
            {
                throw new CustomException($"套餐[{planCode}]不存在或不可用");
            }

            Context.Updateable<SysTenantPlanBinding>()
                .SetColumns(x => new SysTenantPlanBinding
                {
                    Status = 1,
                    Update_by = operatorName,
                    Update_time = DateTime.Now
                })
                .Where(x => x.TenantId == tenantId && x.DelFlag == DelFlagEnum.Normal && x.Status == 0)
                .ExecuteCommand();

            Context.Insertable(new SysTenantPlanBinding
            {
                TenantId = tenantId,
                PlanCode = planCode,
                Status = 0,
                StartTime = dto.StartTime ?? DateTime.Now,
                EndTime = dto.EndTime,
                MaxUsersOverride = dto.MaxUsersOverride,
                DelFlag = DelFlagEnum.Normal,
                Remark = dto.Remark,
                Create_by = operatorName,
                Create_time = DateTime.Now
            }).ExecuteCommand();

            CacheHelper.Remove($"tenant:plan:perms:{tenantId}");
            SendTenantAdminMessage(tenantId, "您的租户套餐已变更，相关功能权限已同步更新。");

            InsertTenantOrder(tenantId, "plan-assign", planCode,
                dto.StartTime ?? DateTime.Now, dto.EndTime, dto.Amount, operatorName,
                string.IsNullOrWhiteSpace(dto.Remark) ? $"分配套餐[{planCode}]" : dto.Remark);

            return GetCurrentTenantPlan(tenantId);
        }

        public void EnsureTenantUserQuotaForAdd(string tenantId, int addingCount = 1)
        {
            // 未启用多租户时不做配额校验，直接放行
            if (!App.IsTenantEnabled())
            {
                return;
            }

            if (addingCount <= 0)
            {
                return;
            }

            var plan = GetCurrentTenantPlan(tenantId);
            if (plan == null || plan.MaxUsers < 0)
            {
                return;
            }

            if (plan.CurrentUsers + addingCount > plan.MaxUsers)
            {
                throw new CustomException(ResultCode.DENY, $"当前租户套餐用户上限为{plan.MaxUsers}，现有{plan.CurrentUsers}，新增{addingCount}后将超限", false);
            }
        }

        public TenantUsageDashboardDto GetTenantUsageDashboard(string tenantId)
        {
            EnsureDefaultPlans();

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                tenantId = App.GetCurrentTenantId();
            }

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new CustomException("租户标识不能为空");
            }

            var tenant = GetByTenantId(tenantId);
            if (tenant == null)
            {
                throw new CustomException("租户不存在");
            }

            var plan = GetCurrentTenantPlan(tenantId);
            var daysToExpire = tenant.ExpireTime.HasValue
                ? (int?)Math.Ceiling((tenant.ExpireTime.Value - DateTime.Now).TotalDays)
                : null;

            var isExpired = tenant.ExpireTime.HasValue && tenant.ExpireTime.Value < DateTime.Now;
            var expireSoon = !isExpired && daysToExpire.HasValue && daysToExpire.Value <= 30;

            var usageRate = plan != null && plan.MaxUsers > 0
                ? Math.Round(plan.CurrentUsers * 100M / plan.MaxUsers, 2)
                : 0M;

            return new TenantUsageDashboardDto
            {
                TenantId = tenant.TenantId,
                TenantName = tenant.TenantName,
                Domain = tenant.Domain,
                Logo = tenant.Logo,
                CompanyName = tenant.CompanyName,
                ContactName = tenant.ContactName,
                ContactPhone = tenant.ContactPhone,
                TenantStatus = tenant.Status,
                ExpireTime = tenant.ExpireTime,
                DaysToExpire = daysToExpire,
                ExpireSoon = expireSoon,
                IsExpired = isExpired,
                PlanCode = plan?.PlanCode,
                PlanName = plan?.PlanName,
                MaxUsers = plan?.MaxUsers ?? 0,
                CurrentUsers = plan?.CurrentUsers ?? 0,
                UserUsageRate = usageRate
            };
        }

        public List<TenantLoginInfoDto> GetLoginTenantList()
        {
            return Queryable()
                .Where(x => x.DelFlag == DelFlagEnum.Normal && x.Status == 0)
                .OrderBy(x => x.TenantName)
                .Select(x => new TenantLoginInfoDto
                {
                    TenantId = x.TenantId,
                    TenantName = x.TenantName,
                    Domain = x.Domain,
                    Logo = x.Logo
                })
                .ToList();
        }

        /// <summary>
        /// 过期租户自动停服：扫描已到期且仍在启用的租户，逐个停服。供定时任务调用。
        /// </summary>
        /// <param name="operatorName">操作人，默认 system（定时任务）</param>
        /// <returns>实际停服的租户数量</returns>
        public int SuspendExpiredTenants(string operatorName = "system")
        {
            EnsureTenantFeatureEnabled();

            var now = DateTime.Now;
            var expired = Queryable()
                .Where(x => x.DelFlag == DelFlagEnum.Normal && x.Status == 0 && x.ExpireTime != null && x.ExpireTime < now)
                .ToList();

            var count = 0;
            foreach (var tenant in expired)
            {
                if (string.Equals(tenant.TenantId, App.MainDbConfigId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                SuspendTenant(tenant.TenantId, operatorName, "到期自动停服");
                count++;
            }

            return count;
        }

        /// <summary>
        /// 向全部/指定启用租户的管理员群发公告站内信。
        /// 指定 TenantIds 时仅发送其中存在且启用的租户；为空时发送全部启用租户。
        /// </summary>
        public int BroadcastToTenants(TenantBroadcastDto dto, string operatorName)
        {
            EnsureTenantFeatureEnabled();

            if (dto == null || string.IsNullOrWhiteSpace(dto.Content))
            {
                throw new CustomException("公告内容不能为空");
            }

            var targets = Queryable()
                .Where(x => x.DelFlag == DelFlagEnum.Normal && x.Status == 0)
                .ToList();
            if (dto.TenantIds is { Count: > 0 })
            {
                var idSet = new HashSet<string>(dto.TenantIds.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()), StringComparer.OrdinalIgnoreCase);
                targets = targets.Where(x => idSet.Contains(x.TenantId)).ToList();
            }

            var content = $"[平台公告]{dto.Content.Trim()}";
            foreach (var tenant in targets)
            {
                SendTenantAdminMessage(tenant.TenantId, content);
            }

            Log.WriteLine(ConsoleColor.Cyan, $"[SysTenantService] 租户公告群发完成：操作人 {operatorName}，发送 {targets.Count} 个租户");
            return targets.Count;
        }

        /// <summary>
        /// 到期阶梯提醒标记前缀。完整格式 [expire-remind:{到期日yyyyMMdd}-{剩余天数}d]，
        /// 写入租户 Remark 用于幂等去重：同一到期日的同一阶段只提醒一次。
        /// </summary>
        private const string ExpireRemindMarkPrefix = "[expire-remind:";

        private static readonly int[] ExpireRemindStages = [30, 15, 7, 3, 1];

        public int RemindExpiringTenants(string operatorName = "system")
        {
            EnsureTenantFeatureEnabled();

            var now = DateTime.Now;
            var maxStage = ExpireRemindStages.Max();
            var candidates = Queryable()
                .Where(x => x.DelFlag == DelFlagEnum.Normal && x.Status == 0
                    && x.ExpireTime != null && x.ExpireTime >= now && x.ExpireTime <= now.AddDays(maxStage))
                .ToList();

            var sent = 0;
            foreach (var tenant in candidates)
            {
                if (string.Equals(tenant.TenantId, App.MainDbConfigId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var daysLeft = (int)Math.Ceiling((tenant.ExpireTime.Value - now).TotalDays);
                var stage = ExpireRemindStages.FirstOrDefault(s => daysLeft <= s);
                if (stage == 0)
                {
                    continue;
                }

                // 同一到期日+同一阶段只提醒一次；续费改变到期日后开启新一轮提醒
                var mark = $"{ExpireRemindMarkPrefix}{tenant.ExpireTime.Value:yyyyMMdd}-{stage}d]";
                if (tenant.Remark != null && tenant.Remark.Contains(mark, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                SendTenantAdminMessage(tenant.TenantId,
                    $"您的租户将于{stage}天后（{tenant.ExpireTime.Value:yyyy-MM-dd}）到期，到期后服务将自动暂停，请及时联系平台管理员续费。");
                SendTenantSms(tenant.ContactPhone, "tenantExpireRemind", new Dictionary<string, string>
                {
                    ["days"] = stage.ToString(),
                    ["expiryDate"] = tenant.ExpireTime.Value.ToString("yyyy-MM-dd")
                });

                tenant.Remark = string.IsNullOrWhiteSpace(tenant.Remark) ? mark : $"{tenant.Remark}{mark}";
                tenant.Update_by = operatorName;
                tenant.Update_time = now;
                Update(tenant, it => new { it.Remark, it.Update_by, it.Update_time });
                sent++;
            }

            return sent;
        }

        /// <summary>
        /// 套餐到期降级通知：扫描已过期且仍为生效状态（Status=0）的套餐绑定，向租户管理员发送
        /// 降级通知，绑定置为 Status=2（已处理降级，幂等标记），并写入 degrade 计费流水。
        /// </summary>
        public int NotifyExpiredPlanBindings(string operatorName = "system")
        {
            EnsureTenantFeatureEnabled();

            var expiredBindings = Context.Queryable<SysTenantPlanBinding>()
                .Where(x => x.DelFlag == DelFlagEnum.Normal && x.Status == 0 && x.EndTime != null && x.EndTime < DateTime.Now)
                .ToList();

            var notified = 0;
            foreach (var binding in expiredBindings)
            {
                if (string.Equals(binding.TenantId, App.MainDbConfigId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                SendTenantAdminMessage(binding.TenantId,
                    $"您的套餐[{binding.PlanCode}]已于{binding.EndTime:yyyy-MM-dd}到期，已自动降级为默认套餐，部分功能可能受限，如需恢复请联系平台管理员续费升级。");
                var bindingTenant = GetByTenantId(binding.TenantId);
                SendTenantSms(bindingTenant?.ContactPhone, "tenantPlanDegraded", new Dictionary<string, string>
                {
                    ["planName"] = binding.PlanCode,
                    ["date"] = binding.EndTime?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd")
                });

                // Status=2 标记已处理降级：与 Status=1（被新绑定顶替失效）区分，同时天然幂等
                Context.Updateable<SysTenantPlanBinding>()
                    .SetColumns(x => new SysTenantPlanBinding { Status = 2, Update_by = operatorName, Update_time = DateTime.Now })
                    .Where(x => x.Id == binding.Id)
                    .ExecuteCommand();

                InsertTenantOrder(binding.TenantId, "degrade", binding.PlanCode,
                    binding.StartTime, binding.EndTime, null, operatorName,
                    $"套餐[{binding.PlanCode}]到期自动降级为默认套餐");

                Log.WriteLine(ConsoleColor.Yellow, $"[SysTenantService] 租户[{binding.TenantId}]套餐[{binding.PlanCode}]到期已降级并通知");
                notified++;
            }

            return notified;
        }

        public List<TenantExpireReminderDto> GetTenantExpireReminders(int withinDays = 30)
        {
            EnsureDefaultPlans();

            if (withinDays <= 0)
            {
                withinDays = 30;
            }

            var now = DateTime.Now;
            var until = now.AddDays(withinDays);

            var tenants = Context.Queryable<SysTenant>()
                .Where(x => x.DelFlag == DelFlagEnum.Normal && x.ExpireTime != null && x.ExpireTime <= until)
                .OrderBy(x => x.ExpireTime)
                .ToList();

            var result = new List<TenantExpireReminderDto>(tenants.Count);
            foreach (var tenant in tenants)
            {
                var plan = GetCurrentTenantPlan(tenant.TenantId);
                var daysToExpire = tenant.ExpireTime.HasValue
                    ? (int?)Math.Ceiling((tenant.ExpireTime.Value - now).TotalDays)
                    : null;
                var isExpired = tenant.ExpireTime.HasValue && tenant.ExpireTime.Value < now;
                var usageRate = plan != null && plan.MaxUsers > 0
                    ? Math.Round(plan.CurrentUsers * 100M / plan.MaxUsers, 2)
                    : 0M;

                result.Add(new TenantExpireReminderDto
                {
                    TenantId = tenant.TenantId,
                    TenantName = tenant.TenantName,
                    ExpireTime = tenant.ExpireTime,
                    DaysToExpire = daysToExpire,
                    IsExpired = isExpired,
                    TenantStatus = tenant.Status,
                    PlanCode = plan?.PlanCode,
                    PlanName = plan?.PlanName,
                    MaxUsers = plan?.MaxUsers ?? 0,
                    CurrentUsers = plan?.CurrentUsers ?? 0,
                    UserUsageRate = usageRate
                });
            }

            return result;
        }

        private static TenantLifecycleResult CreateResult(string tenantId, string action)
        {
            return new TenantLifecycleResult
            {
                TenantId = tenantId,
                Action = action,
                Success = false,
                Message = string.Empty
            };
        }

        private static void AppendStep(TenantLifecycleResult result, string step, bool success, string message)
        {
            result.Steps.Add(new TenantLifecycleStep
            {
                Step = step,
                Success = success,
                Message = message,
                Time = DateTime.Now
            });
        }

        private static string MergeRemark(string currentRemark, string newRemark, string prefix)
        {
            if (string.IsNullOrWhiteSpace(newRemark))
            {
                return currentRemark;
            }

            var remark = $"[{prefix}]{newRemark}";
            if (string.IsNullOrWhiteSpace(currentRemark))
            {
                return remark;
            }

            return $"{currentRemark} | {remark}";
        }

        private static void EnsureTenantFeatureEnabled()
        {
            if (!App.IsTenantEnabled())
            {
                throw new CustomException("当前未启用多租户功能（UseTenant != 1）");
            }
        }

        /// <summary>
        /// 按流水单号查询租户计费流水。
        /// </summary>
        public SysTenantOrder GetTenantOrderByNo(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo)) return null;
            return Context.Queryable<SysTenantOrder>()
                .Where(x => x.OrderNo == orderNo)
                .First();
        }

        /// <summary>
        /// 将计费流水标记为已支付（CAS：仅 PayStatus=0 时生效，防微信重推回调重复入账）。
        /// </summary>
        public bool MarkTenantOrderPaid(string orderNo, string transactionId, string payChannel = "wechat")
        {
            var rows = Context.Updateable<SysTenantOrder>()
                .SetColumns(x => new SysTenantOrder
                {
                    PayStatus = 1,
                    PayChannel = payChannel,
                    TransactionId = transactionId,
                    PayTime = DateTime.Now,
                    Update_time = DateTime.Now
                })
                .Where(x => x.OrderNo == orderNo && x.PayStatus == 0)
                .ExecuteCommand();
            return rows > 0;
        }

        /// <summary>
        /// 创建租户在线续费待支付流水（支付闭环第一步）。
        /// 金额、时长、租户以流水为准，支付回调只认单号。
        /// </summary>
        public SysTenantOrder CreateTenantRenewOrder(string tenantId, int durationDays, decimal amount, string operatorName)
        {
            var orderNo = $"TO{DateTime.Now:yyyyMMddHHmmssfff}{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
            var order = new SysTenantOrder
            {
                OrderNo = orderNo,
                TenantId = tenantId,
                ActionType = "renew",
                Amount = amount,
                OperatorName = operatorName,
                Remark = $"在线续费{durationDays}天（待支付）",
                DurationDays = durationDays,
                PayStatus = 0,
                Create_by = operatorName,
                Create_time = DateTime.Now
            };
            Context.Insertable(order).ExecuteCommand();
            return order;
        }

        /// <summary>
        /// 写入套餐计费流水。流水失败仅记日志不阻断主流程（业务变更已成功，账本可事后补录）。
        /// </summary>
        /// <param name="tenantId">租户标识</param>
        /// <param name="actionType">plan-assign / renew / degrade</param>
        /// <param name="planCode">涉及套餐编码</param>
        /// <param name="startTime">生效开始时间</param>
        /// <param name="endTime">到期时间</param>
        /// <param name="amount">计费金额，可空</param>
        /// <param name="operatorName">操作人</param>
        /// <param name="remark">备注</param>
        private void InsertTenantOrder(string tenantId, string actionType, string planCode,
            DateTime? startTime, DateTime? endTime, decimal? amount, string operatorName, string remark)
        {
            try
            {
                var orderNo = $"TO{DateTime.Now:yyyyMMddHHmmssfff}{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
                Context.Insertable(new SysTenantOrder
                {
                    OrderNo = orderNo,
                    TenantId = tenantId,
                    ActionType = actionType,
                    PlanCode = planCode,
                    StartTime = startTime,
                    EndTime = endTime,
                    Amount = amount,
                    OperatorName = operatorName,
                    Remark = remark,
                    Create_by = operatorName,
                    Create_time = DateTime.Now
                }).ExecuteCommand();
            }
            catch (Exception ex)
            {
                Log.WriteLine(ConsoleColor.Red, $"[SysTenantService] 租户[{tenantId}]计费流水写入失败({actionType}): {ex.Message}");
            }
        }

        /// <summary>
        /// 清除绑定指定套餐的所有租户的套餐权限缓存（key 与 SysTenantPlanMenuService.GetPermsByTenantId 保持一致）。
        /// 套餐删除/菜单变更后调用，避免权限判定沿用旧缓存。
        /// </summary>
        private void ClearTenantPlanPermsCache(string planCode)
        {
            if (string.IsNullOrWhiteSpace(planCode)) return;

            var tenantIds = Context.Queryable<SysTenantPlanBinding>()
                .Where(x => x.PlanCode == planCode && x.DelFlag == DelFlagEnum.Normal)
                .Select(x => x.TenantId)
                .Distinct()
                .ToList();

            foreach (var tid in tenantIds)
            {
                CacheHelper.Remove($"tenant:plan:perms:{tid}");
            }
        }

        private static void EnsureDbConfigExists(string tenantId)
        {
            List<DbConfigs> configs = App.OptionsSetting?.DbConfigs;
            if (configs == null || !configs.Any(x => string.Equals(x.ConfigId, tenantId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new CustomException($"未找到租户[{tenantId}]对应的数据库配置，请先在dbConfigs中配置ConfigId");
            }
        }

        private void EnsureDefaultPlans()
        {
            var hasAny = Context.Queryable<SysTenantPlan>().Any(x => x.DelFlag == DelFlagEnum.Normal);
            if (hasAny)
            {
                return;
            }

            // 首次运行时写入默认套餐，之后可通过后台管理系统自行维护
            var plans = new List<SysTenantPlan>
            {
                new() { PlanCode = "free", PlanName = "免费版", MaxUsers = 5, IsDefault = 1, Sort = 10, Status = 0, DelFlag = DelFlagEnum.Normal, Remark = "系统默认套餐", Create_time = DateTime.Now },
                new() { PlanCode = "pro", PlanName = "专业版", MaxUsers = 100, IsDefault = 0, Sort = 20, Status = 0, DelFlag = DelFlagEnum.Normal, Remark = "系统默认套餐", Create_time = DateTime.Now }
            };
            Context.Insertable(plans).ExecuteCommand();
        }

        /// <summary>
        /// 获取默认套餐的 PlanCode（查数据库 IsDefault=1，无则取第一个，兜底返回 free）
        /// </summary>
        private string GetDefaultPlanCode()
        {
            var plan = Context.Queryable<SysTenantPlan>()
                .Where(x => x.DelFlag == DelFlagEnum.Normal && x.IsDefault == 1)
                .OrderBy(x => x.Sort)
                .First();
            if (plan != null) return plan.PlanCode;

            plan = Context.Queryable<SysTenantPlan>()
                .Where(x => x.DelFlag == DelFlagEnum.Normal)
                .OrderBy(x => x.Sort)
                .First();
            return plan?.PlanCode ?? "free";
        }

        private SysTenantPlanBinding ResolveActiveTenantPlanBinding(string tenantId)
        {
            return Context.Queryable<SysTenantPlanBinding>()
                .Where(x => x.TenantId == tenantId && x.DelFlag == DelFlagEnum.Normal && x.Status == 0)
                .OrderBy(x => x.Id, OrderByType.Desc)
                .First(x => x.EndTime == null || x.EndTime >= DateTime.Now);
        }

        private SysTenantPlan ResolvePlanByBindingOrDefault(SysTenantPlanBinding binding)
        {
            if (binding != null)
            {
                var activePlan = Context.Queryable<SysTenantPlan>()
                    .First(x => x.PlanCode == binding.PlanCode && x.DelFlag == DelFlagEnum.Normal && x.Status == 0);
                if (activePlan != null)
                {
                    return activePlan;
                }
            }

            return Context.Queryable<SysTenantPlan>()
                .OrderBy(x => x.Sort)
                .OrderBy(x => x.Id)
                .First(x => x.DelFlag == DelFlagEnum.Normal && x.Status == 0 && x.IsDefault == 1)
                ?? Context.Queryable<SysTenantPlan>()
                    .OrderBy(x => x.Sort)
                    .OrderBy(x => x.Id)
                    .First(x => x.DelFlag == DelFlagEnum.Normal && x.Status == 0);
        }

        private string SeedTenantBaseDataFromMainDb(string tenantId)
        {
            var mainDb = App.MainDbConfigId;
            if (string.Equals(mainDb, tenantId, StringComparison.OrdinalIgnoreCase))
            {
                return "当前租户即主库，跳过种子复制";
            }

            var targetDb = ResolveTenantDb(tenantId);

            // 当租户库已有核心数据时默认不覆盖，避免误伤线上数据。
            // 注：SysMenu 是主库表（IMainDbEntity），租户库不保存副本，判重只看租户库自有表。
            if (targetDb.Queryable<SysUser>().Any())
            {
                return "租户库已存在基础数据，跳过种子复制";
            }

            // 从 Excel 种子文件读取数据（与主库数据隔离，避免泄漏平台专有数据）
            var wwwRoot = App.WebHostEnvironment?.WebRootPath;
            if (string.IsNullOrWhiteSpace(wwwRoot))
            {
                return "WebRootPath 不可用，无法读取种子文件";
            }

            var path = Path.Combine(wwwRoot, "data.xlsx");
            if (!File.Exists(path))
            {
                return $"种子文件不存在: {path}";
            }

            var logs = new List<string>();

            // 读取 Excel（与 InitSeedData 保持一致的数据源）
            // 注：sys_menu 为 IMainDbEntity 主库表，租户库不保存副本，菜单权限由主库 sys_tenant_plan_menu 套餐机制提供，故不复制菜单。
            var sysDept = MiniExcel.Query<SysDept>(path, sheetName: "dept").ToList();
            var sysPost = MiniExcel.Query<SysPost>(path, sheetName: "post").ToList();
            var sysRole = MiniExcel.Query<SysRole>(path, sheetName: "role").ToList();
            var sysRoleMenu = MiniExcel.Query<SysRoleMenu>(path, sheetName: "role_menu").ToList();
            var sysUser = MiniExcel.Query<SysUser>(path, sheetName: "user").ToList();
            sysUser.ForEach(x => x.Password = "E10ADC3949BA59ABBE56E057F20F883E");
            var sysUserRole = MiniExcel.Query<SysUserRole>(path, sheetName: "user_role").ToList();

            try
            {
                targetDb.Ado.BeginTran();

                var deptStore = targetDb.Storageable(sysDept)
                    .WhereColumns(it => it.DeptId).ToStorage();
                deptStore.AsInsertable.OffIdentity().ExecuteCommand();
                logs.Add($"部门:{deptStore.InsertList.Count}");

                var postStore = targetDb.Storageable(sysPost)
                    .WhereColumns(it => it.PostCode).ToStorage();
                postStore.AsInsertable.ExecuteCommand();
                logs.Add($"岗位:{postStore.InsertList.Count}");

                var roleStore = targetDb.Storageable(sysRole)
                    .WhereColumns(it => it.RoleKey).ToStorage();
                roleStore.AsInsertable.OffIdentity().ExecuteCommand();
                logs.Add($"角色:{roleStore.InsertList.Count}");

                var roleMenuStore = targetDb.Storageable(sysRoleMenu)
                    .WhereColumns(it => new { it.Role_id, it.Menu_id }).ToStorage();
                roleMenuStore.AsInsertable.ExecuteCommand();
                logs.Add($"角色菜单:{roleMenuStore.InsertList.Count}");

                var userStore = targetDb.Storageable(sysUser)
                    .WhereColumns(it => it.UserId).ToStorage();
                userStore.AsInsertable.OffIdentity().ExecuteCommand();
                logs.Add($"用户:{userStore.InsertList.Count}");

                var userRoleStore = targetDb.Storageable(sysUserRole)
                    .WhereColumns(it => new { it.UserId, it.RoleId }).ToStorage();
                userRoleStore.AsInsertable.ExecuteCommand();
                logs.Add($"用户角色:{userRoleStore.InsertList.Count}");

                // 注意：SysDictType / SysDictData / SysConfig / SysTasks / SysTasksLog 是主库实体（IMainDbEntity），租户库不保存副本

                targetDb.Ado.CommitTran();

                var sb = new StringBuilder("已从Excel种子文件初始化数据 -> ");
                sb.Append(string.Join("; ", logs));
                return sb.ToString();
            }
            catch (Exception ex)
            {
                targetDb.Ado.RollbackTran();
                return $"种子数据初始化失败，事务已回滚: {ex.Message}";
            }
        }

        private string SeedTenantPermissionDataFromMainDb(string tenantId)
        {
            var mainDb = App.MainDbConfigId;
            if (string.Equals(mainDb, tenantId, StringComparison.OrdinalIgnoreCase))
            {
                return "当前租户即主库，跳过权限菜单初始化";
            }

            var sourceDb = ResolveMainDb();
            var targetDb = ResolveTenantDb(tenantId);

            // SysMenu 为 IMainDbEntity 主库表，租户库不保存副本，判重只看租户库自有表。
            if (targetDb.Queryable<SysRole>().Any())
            {
                return "租户库已存在权限菜单数据，跳过初始化";
            }

            var logs = new List<string>();

            var deptStore = targetDb.Storageable(sourceDb.Queryable<SysDept>().ToList())
                .WhereColumns(it => it.DeptId)
                .ToStorage();
            deptStore.AsInsertable.OffIdentity().ExecuteCommand();
            logs.Add($"部门:{deptStore.InsertList.Count}");

            var postStore = targetDb.Storageable(sourceDb.Queryable<SysPost>().ToList())
                .WhereColumns(it => it.PostCode)
                .ToStorage();
            postStore.AsInsertable.ExecuteCommand();
            logs.Add($"岗位:{postStore.InsertList.Count}");

            var roleStore = targetDb.Storageable(sourceDb.Queryable<SysRole>().ToList())
                .WhereColumns(it => it.RoleKey)
                .ToStorage();
            roleStore.AsInsertable.OffIdentity().ExecuteCommand();
            logs.Add($"角色:{roleStore.InsertList.Count}");

            var roleMenuStore = targetDb.Storageable(sourceDb.Queryable<SysRoleMenu>().ToList())
                .WhereColumns(it => new { it.Role_id, it.Menu_id })
                .ToStorage();
            roleMenuStore.AsInsertable.ExecuteCommand();
            logs.Add($"角色菜单:{roleMenuStore.InsertList.Count}");

            var roleDeptStore = targetDb.Storageable(sourceDb.Queryable<SysRoleDept>().ToList())
                .WhereColumns(it => new { it.RoleId, it.DeptId })
                .ToStorage();
            roleDeptStore.AsInsertable.ExecuteCommand();
            logs.Add($"角色部门:{roleDeptStore.InsertList.Count}");

            var userStore = targetDb.Storageable(sourceDb.Queryable<SysUser>().Where(x => x.UserId == 1).ToList())
                .WhereColumns(it => it.UserId)
                .ToStorage();
            userStore.AsInsertable.OffIdentity().ExecuteCommand();
            logs.Add($"管理员用户:{userStore.InsertList.Count}");

            var userRoleStore = targetDb.Storageable(sourceDb.Queryable<SysUserRole>().Where(x => x.UserId == 1).ToList())
                .WhereColumns(it => new { it.UserId, it.RoleId })
                .ToStorage();
            userRoleStore.AsInsertable.ExecuteCommand();
            logs.Add($"管理员角色:{userRoleStore.InsertList.Count}");

            var userPostStore = targetDb.Storageable(sourceDb.Queryable<SysUserPost>().Where(x => x.UserId == 1).ToList())
                .WhereColumns(it => new { it.UserId, it.PostId })
                .ToStorage();
            userPostStore.AsInsertable.ExecuteCommand();
            logs.Add($"管理员岗位:{userPostStore.InsertList.Count}");

            return "已初始化权限菜单数据 -> " + string.Join("; ", logs);
        }
    }
}

