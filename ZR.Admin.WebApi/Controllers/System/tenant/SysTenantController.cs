using Microsoft.AspNetCore.Mvc;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Model.System.Tenant;

namespace ZR.Admin.WebApi.Controllers.System.tenant
{
    /// <summary>
    /// 租户管理
    /// </summary>
    [Route("system/tenant")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SysTenantController : BaseController
    {
        private readonly ISysTenantService _sysTenantService;

        public SysTenantController(ISysTenantService sysTenantService)
        {
            _sysTenantService = sysTenantService;
        }

        /// <summary>
        /// 查询租户列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "system:tenant:list")]
        public IActionResult QueryList([FromQuery] SysTenantQueryDto parm)
        {
            var response = _sysTenantService.GetPageList(parm);
            return SUCCESS(response);
        }

        /// <summary>
        /// 查询租户到期提醒。
        /// </summary>
        /// <param name="withinDays"></param>
        /// <returns></returns>
        [HttpGet("expire/reminders")]
        [ActionPermissionFilter(Permission = "system:tenant:list")]
        public IActionResult ExpireReminders(int withinDays = 30)
        {
            return SUCCESS(_sysTenantService.GetTenantExpireReminders(withinDays));
        }

        /// <summary>
        /// 当前租户查看本租户信息（到期时间、套餐用量等）
        /// </summary>
        /// <returns></returns>
        [HttpGet("my")]
        [ActionPermissionFilter(Permission = "tenant:my")]
        public IActionResult My()
        {
            if (!App.IsTenantEnabled())
            {
                return ToResponse(ResultCode.FAIL, "多租户未启用");
            }

            var tenantId = App.GetCurrentTenantId();
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return ToResponse(ResultCode.FAIL, "无法获取当前租户标识");
            }

            var dashboard = _sysTenantService.GetTenantUsageDashboard(tenantId);
            return SUCCESS(dashboard);
        }

        /// <summary>
        /// 查询租户详情
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        [ActionPermissionFilter(Permission = "system:tenant:query")]
        public IActionResult GetInfo(long id)
        {
            var response = _sysTenantService.GetFirst(x => x.Id == id && x.DelFlag == 0);
            return SUCCESS(response);
        }

        /// <summary>
        /// 新增租户
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost]
        [ActionPermissionFilter(Permission = "system:tenant:add")]
        [Log(Title = "租户管理", BusinessType = BusinessType.INSERT)]
        public IActionResult Add([FromBody] SysTenantDto dto)
        {
            if (dto == null)
            {
                throw new CustomException("请求参数错误");
            }
            if (string.IsNullOrWhiteSpace(dto.TenantId))
            {
                return ToResponse(ApiResult.Error("租户标识不能为空"));
            }

            var model = dto.Adapt<SysTenant>().ToCreate(HttpContext);
            model.DelFlag = 0;
            if (UserConstants.NOT_UNIQUE.Equals(_sysTenantService.CheckTenantIdUnique(model)))
            {
                return ToResponse(ApiResult.Error($"新增租户[{model.TenantId}]失败，租户标识已存在"));
            }

            if (UserConstants.NOT_UNIQUE.Equals(_sysTenantService.CheckDomainUnique(model.Domain, model.Id)))
            {
                return ToResponse(ApiResult.Error($"新增租户失败，域名绑定[{model.Domain}]已被其他租户占用"));
            }

            var result = _sysTenantService.Insert(model);
            _sysTenantService.RemoveDomainMapCache();
            return SUCCESS(result);
        }

        /// <summary>
        /// 租户开通（自动编排：建档 + 可选初始化）。
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("provision")]
        [ActionPermissionFilter(Permission = "system:tenant:add")]
        [Log(Title = "租户开通", BusinessType = BusinessType.INSERT)]
        public IActionResult Provision([FromBody] TenantProvisionDto dto)
        {
            if (dto == null)
            {
                throw new CustomException("请求参数错误");
            }

            var response = _sysTenantService.ProvisionTenant(dto, HttpContext.GetName());
            return SUCCESS(response);
        }

        /// <summary>
        /// 修改租户
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPut]
        [ActionPermissionFilter(Permission = "system:tenant:update")]
        [Log(Title = "租户管理", BusinessType = BusinessType.UPDATE)]
        public IActionResult Edit([FromBody] SysTenantDto dto)
        {
            if (dto == null || dto.Id <= 0)
            {
                throw new CustomException("请求实体不能为空");
            }
            if (string.IsNullOrWhiteSpace(dto.TenantId))
            {
                return ToResponse(ApiResult.Error("租户标识不能为空"));
            }

            var model = dto.Adapt<SysTenant>().ToUpdate(HttpContext);
            if (UserConstants.NOT_UNIQUE.Equals(_sysTenantService.CheckTenantIdUnique(model)))
            {
                return ToResponse(ApiResult.Error($"修改租户[{model.TenantId}]失败，租户标识已存在"));
            }

            if (UserConstants.NOT_UNIQUE.Equals(_sysTenantService.CheckDomainUnique(model.Domain, model.Id)))
            {
                return ToResponse(ApiResult.Error($"修改租户失败，域名绑定[{model.Domain}]已被其他租户占用"));
            }

            var response = _sysTenantService.Update(w => w.Id == model.Id && w.DelFlag == 0, it => new SysTenant
            {
                TenantName = model.TenantName,
                TenantId = model.TenantId,
                Domain = model.Domain,
                Status = model.Status,
                ExpireTime = model.ExpireTime,
                Remark = model.Remark,
                Update_by = model.Update_by,
                Update_time = model.Update_time
            });

            _sysTenantService.RemoveDomainMapCache();
            return SUCCESS(response);
        }

        /// <summary>
        /// 修改租户状态
        /// </summary>
        /// <param name="id"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        [HttpPut("changeStatus")]
        [ActionPermissionFilter(Permission = "system:tenant:update")]
        [Log(Title = "租户管理", BusinessType = BusinessType.UPDATE)]
        public IActionResult ChangeStatus(long id, int status)
        {
            var model = _sysTenantService.GetFirst(x => x.Id == id && x.DelFlag == 0);
            if (model == null)
            {
                return ToResponse(ApiResult.Error("租户不存在"));
            }

            if (string.Equals(model.TenantId, App.MainDbConfigId, StringComparison.OrdinalIgnoreCase) && status == 1)
            {
                return ToResponse(ApiResult.Error("默认租户不允许停用"));
            }

            model.Status = status;
            model = model.ToUpdate(HttpContext);
            return SUCCESS(_sysTenantService.Update(model, it => new { it.Status, it.Update_by, it.Update_time }));
        }

        /// <summary>
        /// 租户初始化（数据库连通与基础表初始化）。
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("initialize")]
        [ActionPermissionFilter(Permission = "system:tenant:update")]
        [Log(Title = "租户初始化", BusinessType = BusinessType.UPDATE)]
        public IActionResult Initialize([FromBody] TenantInitializeDto dto)
        {
            if (dto == null)
            {
                throw new CustomException("请求参数错误");
            }

            var response = _sysTenantService.InitializeTenant(dto);
            return SUCCESS(response);
        }

        /// <summary>
        /// 租户停服。
        /// </summary>
        /// <param name="tenantId"></param>
        /// <param name="remark"></param>
        /// <returns></returns>
        [HttpPost("suspend")]
        [ActionPermissionFilter(Permission = "system:tenant:update")]
        [Log(Title = "租户停服", BusinessType = BusinessType.UPDATE)]
        public IActionResult Suspend(string tenantId, string? remark = null)
        {
            var response = _sysTenantService.SuspendTenant(tenantId, HttpContext.GetName(), remark);
            return SUCCESS(response);
        }

        /// <summary>
        /// 租户续费。
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("renew")]
        [ActionPermissionFilter(Permission = "system:tenant:update")]
        [Log(Title = "租户续费", BusinessType = BusinessType.UPDATE)]
        public IActionResult Renew([FromBody] TenantRenewDto dto)
        {
            if (dto == null)
            {
                throw new CustomException("请求参数错误");
            }

            var response = _sysTenantService.RenewTenant(dto, HttpContext.GetName());
            return SUCCESS(response);
        }

        /// <summary>
        /// 租户删除（默认停服保留，可选删除记录）。
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("decommission")]
        [ActionPermissionFilter(Permission = "system:tenant:remove")]
        [Log(Title = "租户删除", BusinessType = BusinessType.DELETE)]
        public IActionResult Decommission([FromBody] TenantDecommissionDto dto)
        {
            if (dto == null)
            {
                throw new CustomException("请求参数错误");
            }

            var response = _sysTenantService.DecommissionTenant(dto, HttpContext.GetName());
            return SUCCESS(response);
        }

        /// <summary>
        /// 删除租户
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        [HttpDelete("{ids}")]
        [ActionPermissionFilter(Permission = "system:tenant:remove")]
        [Log(Title = "租户管理", BusinessType = BusinessType.DELETE)]
        public IActionResult Remove(string ids)
        {
            long[] idArr = Tools.SpitLongArrary(ids);
            if (idArr.Length <= 0)
            {
                return ToResponse(ApiResult.Error("删除失败，ID不能为空"));
            }

            var mainDb = App.MainDbConfigId;
            var hasMainTenant = _sysTenantService.Queryable().Any(x => idArr.Contains(x.Id) && x.TenantId == mainDb && x.DelFlag == 0);
            if (hasMainTenant)
            {
                return ToResponse(ApiResult.Error("默认租户不允许删除"));
            }

            var response = _sysTenantService.Update(x => idArr.Contains(x.Id) && x.DelFlag == 0, x => new SysTenant
            {
                DelFlag = 2,
                Update_by = HttpContext.GetName(),
                Update_time = DateTime.Now
            });

            return SUCCESS(response);
        }
    }
}
