using Microsoft.AspNetCore.Mvc;
using MiniExcelLibs;
using SqlSugar;
using ZR.Admin.WebApi.Filters;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 用户管理
    /// </summary>
    [Verify]
    [Route("system/user")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SysUserController : BaseController
    {
        private readonly ISysUserService UserService;
        private readonly ISysRoleService RoleService;
        private readonly ISysPostService PostService;
        private readonly ISysUserPostService UserPostService;

        public SysUserController(
            ISysUserService userService,
            ISysRoleService roleService,
            ISysPostService postService,
            ISysUserPostService userPostService)
        {
            UserService = userService;
            RoleService = roleService;
            PostService = postService;
            UserPostService = userPostService;
        }

        /// <summary>
        /// 用户管理 -> 获取用户
        /// /system/user/list
        /// </summary>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "system:user:list")]
        [HttpGet("list")]
        public IActionResult List([FromQuery] SysUserQueryDto user, PagerInfo pager)
        {
            var list = UserService.SelectUserList(user, pager);

            return SUCCESS(list);
        }

        /// <summary>
        /// 用户管理 -> 编辑、添加用户获取用户，信息查询
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        [HttpGet("")]
        [HttpGet("{userId:int=0}")]
        [ActionPermissionFilter(Permission = "system:user:query")]
        public IActionResult GetInfo(int userId)
        {
            Dictionary<string, object> dic = new();
            var roles = RoleService.SelectRoleAll();
            dic.Add("roles", roles);
            //dic.Add("roles", SysUser.IsAdmin(userId) ? roles : roles.FindAll(f => !f.IsAdmin()));
            dic.Add("posts", PostService.GetAll());

            //编辑
            if (userId > 0)
            {
                SysUser sysUser = UserService.SelectUserById(userId);
                dic.Add("user", sysUser);
                dic.Add("postIds", UserPostService.GetUserPostsByUserId(userId));
                dic.Add("roleIds", sysUser.RoleIds);
            }

            return SUCCESS(dic);
        }

        /// <summary>
        /// 添加用户
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpPost("add")]
        [Log(Title = "用户管理", BusinessType = BusinessType.INSERT)]
        [ActionPermissionFilter(Permission = "system:user:add")]
        public IActionResult AddUser([FromBody] SysUserDto parm)
        {
            var user = parm.Adapt<SysUser>().ToCreate(HttpContext);
            if (user == null) { return ToResponse(ApiResult.Error(101, "请求参数错误")); }
            if (UserConstants.NOT_UNIQUE.Equals(UserService.CheckUserNameUnique(user.UserName)))
            {
                return ToResponse(ApiResult.Error($"新增用户 '{user.UserName}'失败，登录账号已存在"));
            }

            user.Password = NETCore.Encrypt.EncryptProvider.Md5(user.Password);

            return SUCCESS(UserService.InsertUser(user));
        }

        /// <summary>
        /// 修改用户
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpPut("edit")]
        [Log(Title = "用户管理", BusinessType = BusinessType.UPDATE)]
        [ActionPermissionFilter(Permission = "system:user:edit")]
        public IActionResult UpdateUser([FromBody] SysUserDto parm)
        {
            var user = parm.Adapt<SysUser>().ToUpdate(HttpContext);
            if (user == null || user.UserId <= 0) { return ToResponse(ApiResult.Error(101, "请求参数错误")); }

            int upResult = UserService.UpdateUser(user);

            return ToResponse(upResult);
        }

        /// <summary>
        /// 改变用户状态
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [HttpPut("changeStatus")]
        [Log(Title = "修改用户状态", BusinessType = BusinessType.UPDATE)]
        [ActionPermissionFilter(Permission = "system:user:update")]
        public IActionResult ChangeStatus([FromBody] SysUser user)
        {
            if (user == null) { return ToResponse(ApiResult.Error(101, "请求参数错误")); }

            int result = UserService.ChangeUserStatus(user);
            return ToResponse(result);
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        [HttpDelete("{userId}")]
        [Log(Title = "用户管理", BusinessType = BusinessType.DELETE)]
        [ActionPermissionFilter(Permission = "system:user:remove")]
        public IActionResult Remove(int userid = 0)
        {
            if (userid <= 0) { return ToResponse(ApiResult.Error(101, "请求参数错误")); }
            if (userid == 1) return ToResponse(ResultCode.FAIL, "不能删除管理员账号");
            int result = UserService.DeleteUser(userid);

            return ToResponse(result);
        }

        /// <summary>
        /// 重置密码
        /// </summary>
        /// <returns></returns>
        [HttpPut("resetPwd")]
        [Log(Title = "重置密码", BusinessType = BusinessType.UPDATE)]
        [ActionPermissionFilter(Permission = "system:user:resetPwd")]
        public IActionResult ResetPwd([FromBody] SysUserDto sysUser)
        {
            //密码md5
            sysUser.Password = NETCore.Encrypt.EncryptProvider.Md5(sysUser.Password);

            int result = UserService.ResetPwd(sysUser.UserId, sysUser.Password);
            return ToResponse(result);
        }

        /// <summary>
        /// 导入
        /// </summary>
        /// <param name="formFile">使用IFromFile必须使用name属性否则获取不到文件</param>
        /// <returns></returns>
        [HttpPost("importData")]
        [Log(Title = "用户导入", BusinessType = BusinessType.IMPORT, IsSaveRequestData = false, IsSaveResponseData = true)]
        [ActionPermissionFilter(Permission = "system:user:import")]
        public IActionResult ImportData([FromForm(Name = "file")] IFormFile formFile)
        {
            List<SysUser> users = new();
            using (var stream = formFile.OpenReadStream())
            {
                users = stream.Query<SysUser>(startCell: "A2").ToList();
            }

            return SUCCESS(UserService.ImportUsers(users));
        }

        /// <summary>
        /// 用户导入模板下载
        /// </summary>
        /// <returns></returns>
        [HttpGet("importTemplate")]
        [Log(Title = "用户模板", BusinessType = BusinessType.EXPORT, IsSaveRequestData = true, IsSaveResponseData = false)]
        [AllowAnonymous]
        public IActionResult ImportTemplateExcel()
        {
            (string, string) result = DownloadImportTemplate("user");
            return ExportExcel(result.Item2, result.Item1);
        }

        /// <summary>
        /// 用户导出
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [HttpGet("export")]
        [Log(Title = "用户导出", BusinessType = BusinessType.EXPORT)]
        [ActionPermissionFilter(Permission = "system:user:export")]
        public IActionResult UserExport([FromQuery] SysUserQueryDto user)
        {
            var list = UserService.SelectUserList(user, new PagerInfo(1, 10000));

            var result = ExportExcelMini(list.Result, "user", "用户列表");
            return ExportExcel(result.Item2, result.Item1);
        }
    }
}
