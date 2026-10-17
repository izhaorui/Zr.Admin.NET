using Microsoft.AspNetCore.Mvc;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 个人中心
    /// </summary>
    [Route("system/user/profile")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SysProfileController : BaseController
    {
        private readonly ISysUserService UserService;
        private readonly ISysRoleService RoleService;
        private readonly ISysUserPostService UserPostService;
        private readonly ISysDeptService DeptService;
        private readonly ISysFileService FileService;
        private readonly ISysTwoFactorService TwoFactorService;
        private IWebHostEnvironment hostEnvironment;

        public SysProfileController(
            ISysUserService userService,
            ISysRoleService roleService,
            ISysUserPostService postService,
            ISysDeptService deptService,
            ISysFileService sysFileService,
            IWebHostEnvironment hostEnvironment,
            ISysTwoFactorService twoFactorService)
        {
            UserService = userService;
            RoleService = roleService;
            UserPostService = postService;
            DeptService = deptService;
            FileService = sysFileService;
            this.hostEnvironment = hostEnvironment;
            TwoFactorService = twoFactorService;
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
            if (UserService.ResetPwd(userId, newMd5, "您的登录密码修改成功") > 0)
            {
                // 改密往往正是因为怀疑密码泄露：必须同时清空设备信任，
                // 否则"记住此设备"的信任期（默认 7 天）会让已记住的设备继续免动态码
                TwoFactorService.HandlePasswordChanged(userId);

                return SUCCESS(1);
            }

            return ToResponse(ApiResult.Error("修改密码异常，请联系管理员"));
        }

        #region 双因子认证（TOTP）

        /// <summary>
        /// 查询当前账号的双因子绑定状态
        /// </summary>
        [HttpGet("twoFactor")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult GetTwoFactorStatus()
        {
            return SUCCESS(TwoFactorService.GetStatus(HttpContext.GetUId()));
        }

        /// <summary>
        /// 生成待绑定密钥与 otpauth 地址（前端据此本地渲染二维码）。
        /// 此时尚未启用，需再调 enable 校验一次动态码。
        /// </summary>
        [HttpPost("twoFactor/init")]
        [ActionPermissionFilter(Permission = "common")]
        // 响应含明文 TOTP 密钥，绝不能落操作日志（含 NLog 文件）
        [Log(Title = "双因子-获取密钥", BusinessType = BusinessType.UPDATE, IsSaveRequestData = false, IsSaveResponseData = false)]
        public IActionResult InitTwoFactor()
        {
            return SUCCESS(TwoFactorService.Init(HttpContext.GetUId(), HttpContext.GetName()));
        }

        /// <summary>
        /// 启用双因子。需密码 + 认证器动态码双校验，返回一次性恢复码（仅此一次返回）
        /// </summary>
        [HttpPost("twoFactor/enable")]
        [ActionPermissionFilter(Permission = "common")]
        // 请求含密码与动态码；响应含 10 个一次性恢复码明文（未使用即有效），两者都不得落日志
        [Log(Title = "双因子-启用", BusinessType = BusinessType.UPDATE, IsSaveRequestData = false, IsSaveResponseData = false)]
        public IActionResult EnableTwoFactor([FromBody] TwoFactorEnableDto dto)
        {
            if (dto == null) throw new CustomException("请求参数错误");

            return SUCCESS(TwoFactorService.Enable(HttpContext.GetUId(), dto.Password, dto.Code));
        }

        /// <summary>
        /// 解绑双因子。需密码 + 动态码（或一次性恢复码）；认证器已丢失时请改用管理员重置
        /// </summary>
        [HttpPost("twoFactor/disable")]
        [ActionPermissionFilter(Permission = "common")]
        [Log(Title = "双因子-解绑", BusinessType = BusinessType.UPDATE, IsSaveRequestData = false)]
        public IActionResult DisableTwoFactor([FromBody] TwoFactorDisableDto dto)
        {
            if (dto == null) throw new CustomException("请求参数错误");

            TwoFactorService.Disable(HttpContext.GetUId(), dto.Password, dto.Code);
            return SUCCESS(1);
        }

        /// <summary>
        /// 可信设备（"记住此设备"）列表
        /// </summary>
        [HttpGet("twoFactor/devices")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult GetTwoFactorDevices()
        {
            return SUCCESS(TwoFactorService.GetDevices(HttpContext.GetUId()));
        }

        /// <summary>
        /// 吊销一个可信设备
        /// </summary>
        [HttpDelete("twoFactor/devices/{deviceId}")]
        [ActionPermissionFilter(Permission = "common")]
        [Log(Title = "双因子-吊销设备", BusinessType = BusinessType.DELETE)]
        public IActionResult RemoveTwoFactorDevice(long deviceId)
        {
            return ToResponse(TwoFactorService.RemoveDevice(HttpContext.GetUId(), deviceId) ? 1 : 0);
        }

        #endregion

        /// <summary>
        /// 修改头像
        /// </summary>
        /// <param name="picture"></param>
        /// <returns></returns>
        [HttpPost("Avatar")]
        [Consumes("multipart/form-data")]
        [ActionPermissionFilter(Permission = "common")]
        [Log(Title = "修改头像", BusinessType = BusinessType.UPDATE, IsSaveRequestData = false)]
        public async Task<IActionResult> Avatar(IFormFile picture)
        {
            long userId = HttpContext.GetUId();
            if (picture == null) throw new CustomException("请选择文件");
            Model.Dto.UploadDto dto = new()
            {
                FileDir = "avatar",
                ClassifyType = "avatar",
                UserName = HttpContext.GetName()
            };
            SysFile file = await FileService.SaveFileToLocal(hostEnvironment.WebRootPath, dto, dto.UserName, picture);

            UserService.UpdatePhoto(new SysUser() { Avatar = file.AccessUrl, UserId = userId });
            return SUCCESS(new { imgUrl = file.AccessUrl });
        }
    }
}
