using Microsoft.AspNetCore.Mvc;
using ZR.Model.System.Dto;
using ZR.ServiceCore.Services;

namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// SSO 应用接入管理（登记哪些外部系统可以跳转本系统登录）
    /// </summary>
    [Route("system/sso/app")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SsoAppController : BaseController
    {
        private readonly ISysSsoAppService ssoAppService;

        public SsoAppController(ISysSsoAppService ssoAppService)
        {
            this.ssoAppService = ssoAppService;
        }

        /// <summary>
        /// 应用列表
        /// </summary>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "system:sso:list")]
        public IActionResult List([FromQuery] SsoAppQueryDto parm)
        {
            return SUCCESS(ssoAppService.GetPageList(parm));
        }

        /// <summary>
        /// 应用详情
        /// </summary>
        [HttpGet("{id}")]
        [ActionPermissionFilter(Permission = "system:sso:query")]
        public IActionResult Query(long id)
        {
            var app = ssoAppService.GetId(id);
            if (app == null)
            {
                return ToResponse(ResultCode.FAIL, "应用不存在");
            }

            // 密钥哈希不返回给前端，无任何展示场景
            app.AppSecretHash = null;
            return SUCCESS(app);
        }

        /// <summary>
        /// 新增应用。返回的 AppSecret 明文只出现这一次，需当场交给对接方
        /// </summary>
        [HttpPost]
        [ActionPermissionFilter(Permission = "system:sso:add")]
        [Log(Title = "SSO应用", BusinessType = BusinessType.INSERT)]
        public IActionResult Add([FromBody] SsoAppEditDto parm)
        {
            return SUCCESS(ssoAppService.CreateApp(parm));
        }

        /// <summary>
        /// 修改应用（不会改动密钥）
        /// </summary>
        [HttpPut]
        [ActionPermissionFilter(Permission = "system:sso:edit")]
        [Log(Title = "SSO应用", BusinessType = BusinessType.UPDATE)]
        public IActionResult Edit([FromBody] SsoAppEditDto parm)
        {
            return ToResponse(ssoAppService.UpdateApp(parm));
        }

        /// <summary>
        /// 删除应用
        /// </summary>
        [HttpDelete("{ids}")]
        [ActionPermissionFilter(Permission = "system:sso:remove")]
        [Log(Title = "SSO应用", BusinessType = BusinessType.DELETE)]
        public IActionResult Delete(string ids)
        {
            long[] idArr = Tools.SplitAndConvert<long>(ids);
            return ToResponse(ssoAppService.Delete(idArr));
        }

        /// <summary>
        /// 重置密钥。旧密钥立即失效，新密钥明文只返回一次
        /// </summary>
        [HttpPut("resetsecret/{id}")]
        [ActionPermissionFilter(Permission = "system:sso:resetsecret")]
        [Log(Title = "SSO应用密钥重置", BusinessType = BusinessType.UPDATE)]
        public IActionResult ResetSecret(long id)
        {
            return SUCCESS(ssoAppService.ResetSecret(id));
        }
    }
}