using Microsoft.AspNetCore.Mvc;
using ZR.Admin.WebApi.Filters;
using ZR.Model.System;
using ZR.Model.System.Dto;


namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 个人中心
    /// </summary>
    [Verify]
    [Route("system/user/profile")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SysProfileController : BaseController
    {
        private readonly ISysUserService UserService;
        private readonly ISysRoleService RoleService;
        private readonly ISysUserPostService UserPostService;
        private readonly ISysDeptService DeptService;
        private readonly ISysFileService FileService;
        private IWebHostEnvironment hostEnvironment;

        public SysProfileController(
            ISysUserService userService,
            ISysRoleService roleService,
            ISysUserPostService postService,
            ISysDeptService deptService,
            ISysFileService sysFileService,
            IWebHostEnvironment hostEnvironment)
        {
            UserService = userService;
            RoleService = roleService;
            UserPostService = postService;
            DeptService = deptService;
            FileService = sysFileService;
            this.hostEnvironment = hostEnvironment;
        }

        /// <summary>
        /// 个人中心用户信息获取
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Profile()
        {
            long userId = HttpContext.GetUId();
            var user = UserService.SelectUserById(userId);

            var roles = RoleService.SelectUserRoleNames(userId);
            var postGroup = UserPostService.GetPostsStrByUserId(userId);
            var deptInfo = DeptService.GetFirst(f => f.DeptId == user.DeptId);
            user.DeptName = deptInfo?.DeptName ?? "-";

            return SUCCESS(new { user, roles, postGroup }, TIME_FORMAT_FULL);
        }

        /// <summary>
        /// 修改用户
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [ActionPermissionFilter(Permission = "common")]
        [Log(Title = "修改信息", BusinessType = BusinessType.UPDATE)]
        public IActionResult UpdateProfile([FromBody] SysUserDto userDto)
        {
            if (userDto == null)
            {
                throw new CustomException(ResultCode.PARAM_ERROR, "请求参数错误");
            }
            var user = userDto.Adapt<SysUser>().ToUpdate(HttpContext);

            int result = UserService.ChangeUser(user);
            return ToResponse(result);
        }

        /// <summary>
        /// 修改密码
        /// </summary>
        /// <returns></returns>
        [HttpPut("updatePwd")]
        [ActionPermissionFilter(Permission = "common")]
        [Log(Title = "修改密码", BusinessType = BusinessType.UPDATE, IsSaveRequestData = false)]
        public IActionResult UpdatePwd(string oldPassword, string newPassword)
        {
            long userId = HttpContext.GetUId();
            SysUser user = UserService.GetFirst(f => f.UserId == userId);
            string oldMd5 = NETCore.Encrypt.EncryptProvider.Md5(oldPassword);
            string newMd5 = NETCore.Encrypt.EncryptProvider.Md5(newPassword);

            if (!user.Password.Equals(oldMd5, StringComparison.OrdinalIgnoreCase))
            {
                return ToResponse(ApiResult.Error("修改密码失败，旧密码错误"));
            }
            if (user.Password.Equals(newMd5, StringComparison.OrdinalIgnoreCase))
            {
                return ToResponse(ApiResult.Error("新密码不能和旧密码相同"));
            }
            if (UserService.ResetPwd(userId, newMd5) > 0)
            {
                //TODO 更新缓存

                return SUCCESS(1);
            }

            return ToResponse(ApiResult.Error("修改密码异常，请联系管理员"));
        }

        /// <summary>
        /// 修改头像
        /// </summary>
        /// <param name="formFile"></param>
        /// <returns></returns>
        [HttpPost("Avatar")]
        [ActionPermissionFilter(Permission = "common")]
        [Log(Title = "修改头像", BusinessType = BusinessType.UPDATE, IsSaveRequestData = false)]
        public async Task<IActionResult> Avatar([FromForm(Name = "picture")] IFormFile formFile)
        {
            long userId = HttpContext.GetUId();
            if (formFile == null) throw new CustomException("请选择文件");

            SysFile file = await FileService.SaveFileToLocal(hostEnvironment.WebRootPath, "", "avatar", HttpContext.GetName(), formFile);

            UserService.UpdatePhoto(new SysUser() { Avatar = file.AccessUrl, UserId = userId });
            return SUCCESS(new { imgUrl = file.AccessUrl });
        }
    }
}
