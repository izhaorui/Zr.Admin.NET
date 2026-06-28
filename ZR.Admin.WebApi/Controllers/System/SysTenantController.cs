using Microsoft.AspNetCore.Mvc;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.Admin.WebApi.Controllers.System
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

            return SUCCESS(_sysTenantService.Insert(model));
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

            var response = _sysTenantService.Update(w => w.Id == model.Id && w.DelFlag == 0, it => new SysTenant
            {
                TenantName = model.TenantName,
                TenantId = model.TenantId,
                Status = model.Status,
                ExpireTime = model.ExpireTime,
                Remark = model.Remark,
                Update_by = model.Update_by,
                Update_time = model.Update_time
            });

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

            if (string.Equals(model.TenantId, App.Configuration["MainDb"] ?? "0", StringComparison.OrdinalIgnoreCase) && status == 1)
            {
                return ToResponse(ApiResult.Error("默认租户不允许停用"));
            }

            model.Status = status;
            model = model.ToUpdate(HttpContext);
            return SUCCESS(_sysTenantService.Update(model, it => new { it.Status, it.Update_by, it.Update_time }));
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

            var mainDb = App.Configuration["MainDb"] ?? "0";
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
