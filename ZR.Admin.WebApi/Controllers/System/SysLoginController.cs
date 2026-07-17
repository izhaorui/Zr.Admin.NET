using Infrastructure.Captcha;
using Microsoft.AspNetCore.Mvc;
using ZR.Model.Models;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.ServiceCore.Model.Dto;

namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 登录
    /// </summary>
    [ApiExplorerSettings(GroupName = "sys")]
    public class SysLoginController : BaseController
    {
        private readonly ISysUserService sysUserService;
        private readonly ISysMenuService sysMenuService;
        private readonly ISysLoginService sysLoginService;
        private readonly ISysPermissionService permissionService;
        private readonly ICaptchaProvider captchaProvider;
        private readonly ISysConfigService sysConfigService;
        private readonly ISysRoleService roleService;
        private readonly ISmsCodeLogService smsCodeLogService;
        private readonly ISysTenantService sysTenantService;
        private readonly ISysDeptService deptService;

        public SysLoginController(
            ISysMenuService sysMenuService,
            ISysUserService sysUserService,
            ISysLoginService sysLoginService,
            ISysPermissionService permissionService,
            ISysConfigService configService,
            ISysRoleService sysRoleService,
            ISmsCodeLogService smsCodeLogService,
            ICaptchaProvider captchaProvider,
            ISysTenantService sysTenantService,
            ISysDeptService sysDeptService)
        {
            this.captchaProvider = captchaProvider;
            this.sysMenuService = sysMenuService;
            this.sysUserService = sysUserService;
            this.sysLoginService = sysLoginService;
            this.permissionService = permissionService;
            this.sysConfigService = configService;
            this.smsCodeLogService = smsCodeLogService;
            roleService = sysRoleService;
            this.sysTenantService = sysTenantService;
            deptService = sysDeptService;
        }

        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="loginBody">登录对象</param>
        /// <returns></returns>
        [Route("login")]
        [HttpPost]
        [Log(Title = "登录")]
        [AllowAnonymous]
        public IActionResult Login([FromBody] LoginBodyDto loginBody)
        {
            if (loginBody == null) { throw new CustomException("请求参数错误"); }

            sysTenantService.CheckTenant(loginBody.TenantId);
            using var tenantScope = TenantContext.Change(loginBody.TenantId);

            loginBody.LoginIP = HttpContextExtension.GetClientUserIp(HttpContext);
            SysConfig sysConfig = sysConfigService.GetSysConfigByKey("sys.account.captchaOnOff");
            if (sysConfig?.ConfigValue != "off" && !captchaProvider.Validate(loginBody.Uuid, loginBody.Code))
            {
                return ToResponse(ResultCode.CAPTCHA_ERROR, "验证码错误");
            }

            sysLoginService.CheckLockUser(loginBody.Username);
            string location = HttpContextExtension.GetIpInfo(loginBody.LoginIP);
            var user = sysLoginService.Login(loginBody, new SysLogininfor() { LoginLocation = location });
            string abnormalNotice = sysLoginService.GetAbnormalLoginNotice(user, loginBody.LoginIP);

            List<SysRole> roles = roleService.SelectUserRoleListByUserId(user.UserId);
            //权限集合 eg *:*:*,system:user:list（按套餐菜单过滤）
            List<string> permissions = permissionService.GetMenuPermission(new SysUserDto() { UserId = user.UserId });

            TokenModel loginUser = new(user.Adapt<TokenModel>(), roles.Adapt<List<Roles>>())
            {
                TenantId = loginBody.TenantId,
                Permissions = permissions,
                ScopeType = ComputeScopeType(roles),
            };
            var token = JwtUtil.GenerateJwtToken(JwtUtil.AddClaims(loginUser));
            ApiResult apiResult = new((int)ResultCode.SUCCESS, "success", token);
            apiResult.Put("notice", abnormalNotice);
            
            return ToResponse(apiResult);
        }

        /// <summary>
        /// 注销
        /// </summary>
        /// <returns></returns>
        [Log(Title = "注销")]
        [HttpPost("logout")]
        [AllowAnonymous]
        public IActionResult LogOut()
        {
            var userid = HttpContext.GetUId();
            var name = HttpContext.GetName();

            CacheService.RemoveUserPerms(GlobalConstant.UserPermKEY + userid);
            CacheService.RemoveDataScopeDeptIds(userid);
            return SUCCESS(new { name, id = userid });
        }

        /// <summary>
        /// 获取用户信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("getInfo")]
        public IActionResult GetUserInfo()
        {
            long userId = HttpContext.GetUId();
            var user = sysUserService.SelectUserById(userId);

            //前端校验按钮权限使用
            //角色集合 eg: admin,yunying,common
            List<string> roles = permissionService.GetRolePermission(user);
            //权限集合 eg *:*:*,system:user:list（按套餐菜单过滤）
            List<string> permissions = permissionService.GetMenuPermission(user);
            user.WelcomeContent = GlobalConstant.WelcomeMessages[new Random().Next(0, GlobalConstant.WelcomeMessages.Length)];
            user.Password = string.Empty;
            CacheService.SetUserPerms(GlobalConstant.UserPermKEY + userId, permissions);
            // 数据权限部门 ID 缓存（与权限缓存统一管理，前端登录/刷新后自动刷新）
            var userRoles = roleService.SelectUserRoleListByUserId(userId);
            CacheService.SetDataScopeDeptIds(userId, ComputeDataScopeDeptIds(userRoles, user.DeptId));
            return SUCCESS(new
            {
                user = user.Adapt<SysUserDto>(),
                roles,
                permissions,
                isDefaultModifyPwd = InitPassword(user.PwdUpdateTime),
                isPasswordExpired = CheckPasswordExpire(user.PwdUpdateTime)
            });
        }

        /// <summary>
        /// 获取路由信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("getRouters")]
        public IActionResult GetRouters()
        {
            long uid = HttpContext.GetUId();
            List<SysMenu> menus;

            if (App.IsTenantEnabled())
            {
                var tenantId = App.GetCurrentTenantId();
                var mainDb = App.MainDbConfigId;
                var isMainTenant = string.Equals(tenantId, mainDb, StringComparison.OrdinalIgnoreCase);

                if (isMainTenant)
                {
                    // 主租户：直接查主库菜单
                    menus = sysMenuService.SelectMenuTreeByUserId(uid);
                }
                else
                {
                    // 普通租户：按套餐菜单过滤
                    menus = sysMenuService.SelectMenuTreeByUserIdForTenant(uid, tenantId);

                    // 过滤平台专属菜单
                    menus = TenantFeaturePolicy.FilterPlatformMenusForNonMainTenant(menus, false);
                }
            }
            else
            {
                menus = sysMenuService.SelectMenuTreeByUserId(uid);
            }

            return SUCCESS(sysMenuService.BuildMenus(menus));
        }

        /// <summary>
        /// 获取路由信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("getAppRouters")]
        public IActionResult GetAppRouters(int v = 0)
        {
            long uid = HttpContext.GetUId();
            var perms = permissionService.GetMenuPermission(new SysUserDto() { UserId = uid });

            return SUCCESS(sysMenuService.GetAppMenus(perms, v));
        }

        /// <summary>
        /// 获取多租户信息（登录页使用：是否启用多租户 + 可选租户列表）
        /// </summary>
        /// <returns></returns>
        [HttpGet("tenantInfo")]
        [AllowAnonymous]
        public IActionResult TenantInfo()
        {
            var useTenant = App.IsTenantEnabled();
            var tenants = useTenant
                ? sysTenantService.GetLoginTenantList()
                : new List<TenantLoginInfoDto>();

            return SUCCESS(new { useTenant, tenants });
        }

        /// <summary>
        /// 生成图片验证码
        /// </summary>
        /// <returns></returns>
        [HttpGet("captchaImage")]
        [AllowAnonymous]
        public IActionResult CaptchaImage()
        {
            string uuid = Guid.NewGuid().ToString().Replace("-", "");

            SysConfig sysConfig = sysConfigService.GetSysConfigByKey("sys.account.captchaOnOff");
            var captchaOff = sysConfig?.ConfigValue ?? "0";
            var info = captchaProvider.Generate(uuid, 60);
            var obj = new { captchaOff, uuid, img = info.DataUrl };

            return SUCCESS(obj);
        }

        /// <summary>
        /// 注册
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("/register")]
        [AllowAnonymous]
        [Log(Title = "注册", BusinessType = BusinessType.INSERT)]
        public IActionResult Register([FromBody] RegisterDto dto)
        {
            SysConfig config = sysConfigService.GetSysConfigByKey("sys.account.register");
            if (config?.ConfigValue != "true")
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "当前系统没有开启注册功能！");
            }
            SysConfig sysConfig = sysConfigService.GetSysConfigByKey("sys.account.captchaOnOff");
            if (sysConfig?.ConfigValue != "off" && !captchaProvider.Validate(dto.Uuid, dto.Code))
            {
                return ToResponse(ResultCode.CAPTCHA_ERROR, "验证码错误");
            }
            dto.UserIP = HttpContext.GetClientUserIp();
            SysUser user = sysUserService.Register(dto);
            if (user.UserId > 0)
            {
                return SUCCESS(user);
            }
            return ToResponse(ResultCode.CUSTOM_ERROR, "注册失败，请联系管理员");
        }

        #region 二维码登录

        /// <summary>
        /// 生成二维码
        /// </summary>
        /// <param name="uuid"></param>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        [HttpGet("/GenerateQrcode")]
        [AllowAnonymous]
        public IActionResult GenerateQrcode(string uuid, string deviceId)
        {
            var state = Guid.NewGuid().ToString();
            var dict = new Dictionary<string, object>
            {
                { "state", state }
            };
            CacheService.SetScanLogin(uuid, dict);
            return SUCCESS(new
            {
                status = 1,
                state,
                uuid,
                codeContent = new { uuid, deviceId }// "https://qm.qq.com/cgi-bin/qm/qr?k=kgt4HsckdljU0VM-0kxND6d_igmfuPlL&authKey=r55YUbruiKQ5iwC/folG7KLCmZ++Y4rQVgNlvLbUniUMkbk24Y9+zNuOmOnjAjRc&noverify=0"
            });
        }

        /// <summary>
        /// 轮询判断扫码状态
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("/VerifyScan")]
        [AllowAnonymous]
        public IActionResult VerifyScan([FromBody] ScanDto dto)
        {
            int status = -1;
            object token = string.Empty;
            if (CacheService.GetScanLogin(dto.Uuid) is Dictionary<string, object> str)
            {
                status = 0;
                str.TryGetValue("token", out token);
                if (str.ContainsKey("status") && (string)str.GetValueOrDefault("status") == "success")
                {
                    status = 2;//扫码成功
                    CacheService.RemoveScanLogin(dto.Uuid);
                }
            }

            return SUCCESS(new { status, token });
        }

        /// <summary>
        /// 移动端扫码登录
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("/ScanLogin")]
        [Log(Title = "扫码登录")]
        public IActionResult ScanLogin([FromBody] ScanDto dto)
        {
            if (dto == null) { return ToResponse(ResultCode.CUSTOM_ERROR, "扫码失败"); }
            var name = App.HttpContext.GetName();

            sysLoginService.CheckLockUser(name);

            TokenModel tokenModel = JwtUtil.GetLoginUser(HttpContext);
            if (CacheService.GetScanLogin(dto.Uuid) is not null)
            {
                Dictionary<string, object> dict = new() { };
                dict.Add("status", "success");
                dict.Add("token", JwtUtil.GenerateJwtToken(JwtUtil.AddClaims(tokenModel)));
                CacheService.SetScanLogin(dto.Uuid, dict);

                return SUCCESS(1);
            }
            return ToResponse(ResultCode.FAIL, "二维码已失效");
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("/checkMobile")]
        [Log(Title = "发送短息", BusinessType = BusinessType.INSERT)]
        [AllowAnonymous]
        public IActionResult CheckMobile([FromBody] PhoneLoginDto dto)
        {
            dto.LoginIP = HttpContextExtension.GetClientUserIp(HttpContext);
            var uid = HttpContext.GetUId();
            //SysConfig sysConfig = sysConfigService.GetSysConfigByKey("sys.account.captchaOnOff");
            //if (!captchaProvider.Validate(dto.Uuid, dto.Code, false))
            //{
            //    return ToResponse(ResultCode.CUSTOM_ERROR, "验证码错误");
            //}
            sysTenantService.CheckTenant(dto.TenantId);
            if (dto.SendType == 0)
            {
                var info = sysUserService.GetFirst(f => f.Phonenumber == dto.PhoneNum) ?? throw new CustomException(ResultCode.CUSTOM_ERROR, "该手机号不存在", false);
                uid = info.UserId;
            }
            if (dto.SendType == 1)
            {
                if (sysUserService.CheckPhoneBind(dto.PhoneNum).Count > 0)
                {
                    return ToResponse(ResultCode.CUSTOM_ERROR, "手机号已绑定其他账号");
                }
            }

            string location = HttpContextExtension.GetIpInfo(dto.LoginIP);

            smsCodeLogService.AddSmscodeLog(new SmsCodeLog()
            {
                Userid = uid,
                PhoneNum = dto.PhoneNum.ParseToLong(),
                SendType = dto.SendType,
                UserIP = dto.LoginIP,
                Location = location,
            });

            return SUCCESS(1);
        }

        /// <summary>
        /// 手机号登录
        /// </summary>
        /// <param name="loginBody">登录对象</param>
        /// <returns></returns>
        [Route("PhoneLogin")]
        [HttpPost]
        [Log(Title = "手机号登录")]
        [AllowAnonymous]
        public IActionResult PhoneLogin([FromBody] PhoneLoginDto loginBody)
        {
            if (loginBody == null) { throw new CustomException("请求参数错误"); }
            loginBody.LoginIP = HttpContextExtension.GetClientUserIp(HttpContext);

            if (!CacheService.CheckPhoneCode(loginBody.PhoneNum, loginBody.PhoneCode))
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "短信验证码错误");
            }
            sysTenantService.CheckTenant(loginBody.TenantId);
            var info = sysUserService.GetFirst(f => f.Phonenumber == loginBody.PhoneNum) ?? throw new CustomException(ResultCode.CUSTOM_ERROR, "该手机号不存在", false);
            var infoModel = info.Adapt<SysUserDto>();
            sysLoginService.CheckLockUser(info.UserName);
            string location = HttpContextExtension.GetIpInfo(loginBody.LoginIP);
            var user = sysLoginService.PhoneLogin(loginBody, new SysLogininfor() { LoginLocation = location }, infoModel);

            List<SysRole> roles = roleService.SelectUserRoleListByUserId(user.UserId);
            //权限集合 eg *:*:*,system:user:list（按套餐菜单过滤）
            List<string> permissions = permissionService.GetMenuPermission(user);

            TokenModel loginUser = new(user.Adapt<TokenModel>(), roles.Adapt<List<Roles>>())
            {
                TenantId = loginBody.TenantId,
                Permissions = permissions,
                ScopeType = ComputeScopeType(roles),
            };
            return SUCCESS(JwtUtil.GenerateJwtToken(JwtUtil.AddClaims(loginUser)));
        }

        /// <summary>
        /// 手机号绑定
        /// </summary>
        /// <param name="loginBody"></param>
        /// <returns></returns>
        [Route("/PhoneBind")]
        [HttpPost]
        [Log(Title = "手机号绑定")]
        [AllowAnonymous]
        public IActionResult PhoneBind([FromBody] PhoneLoginDto loginBody)
        {
            if (loginBody == null) { throw new CustomException("请求参数错误"); }
            loginBody.LoginIP = HttpContextExtension.GetClientUserIp(HttpContext);
            var uid = HttpContext.GetUId();
            if (!CacheService.CheckPhoneCode(loginBody.PhoneNum, loginBody.PhoneCode))
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "短信验证码错误");
            }
            var result = sysUserService.ChangePhoneNum(uid, loginBody.PhoneNum);

            return SUCCESS(result);
        }

        /// <summary>
        /// 解锁屏幕
        /// </summary>
        /// <returns></returns>
        [Route("/unlockscreen")]
        [HttpPost]
        [Log(Title = "解锁屏幕")]
        public IActionResult Unlockscreen([FromBody] LoginBodyDto dto)
        {
            if (dto == null || dto.Password.IsEmpty())
            {
                throw new CustomException("密码不能为空");
            }
            dto.Username = HttpContext.GetName();

            if (dto.Password.Length != 32)
            {
                dto.Password = NETCore.Encrypt.EncryptProvider.Md5(dto.Password);
            }
            SysUser user = sysUserService.Login(dto);
            if (user == null)
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "密码错误,请重新输入");
            }
            return SUCCESS(1);
        }

        /// <summary>
        /// 登录时预计算合并后的数据权限等级（取所有角色中最宽松的权限）
        /// </summary>
        private static int ComputeScopeType(List<SysRole> roles)
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

        /// <summary>
        /// 登录时预计算用户数据权限部门集合（DEPT_CHILD + CUSTOM 并集），
        /// 存入 JWT Token 后 QueryFilter 直接读缓存，避免每次查询 SQL EXISTS 子查询。
        /// </summary>
        private List<long> ComputeDataScopeDeptIds(List<SysRole> roles, long deptId)
        {
            var result = new HashSet<long>();

            var isAdmin = roles.Any(r => r.RoleKey == GlobalConstant.AdminRole || r.DataScope == (int)DataPermiEnum.All);
            var deptChildRoles = roles.Where(r => r.DataScope == (int)DataPermiEnum.DEPT_CHILD).ToList();
            var customRoles = roles.Where(r => r.DataScope == (int)DataPermiEnum.CUSTOM).ToList();

            // 非管理员始终包含自己的部门（避免空列表导致 SQL IN() 语法错误）
            if (!isAdmin)
            {
                result.Add(deptId);
            }
            if (deptChildRoles.Any())
            {
                var childIds = deptService.GetChildDeptIds(deptId);
                if (childIds != null) result.UnionWith(childIds);
            }
            if (customRoles.Any())
            {
                var roleIds = customRoles.Select(r => r.RoleId).ToList();
                var customDeptIds = deptService.SelectRoleDeptsBatch(roleIds);
                if (customDeptIds != null) result.UnionWith(customDeptIds);
            }

            // 调试：打印角色信息 + 计算结果
            var roleInfo = string.Join("|", roles.Select(r => $"{r.RoleKey}:Scope={r.DataScope}"));
            NLog.LogManager.GetCurrentClassLogger().Info($"[DataScope] deptId={deptId} roles=[{roleInfo}] DEPT_CHILD={deptChildRoles.Count} CUSTOM={customRoles.Count} DeptIds=[{string.Join(",", result)}]");

            return result.ToList();
        }

        /// <summary>
        /// 检查密码是否提醒
        /// </summary>
        private bool InitPassword(DateTime? pwdUpdateTime)
        {
            var initPasswordModify = sysConfigService.GetSysConfigByKey("sys.account.initPasswordModify");
            return initPasswordModify != null && initPasswordModify.ConfigValue == "1" && pwdUpdateTime == null;
        }

        private bool CheckPasswordExpire(DateTime? pwdUpdateTime)
        {
            var passwordExpireDayConfig = sysConfigService.GetSysConfigByKey("sys.account.passwordExpireDay");
            if (passwordExpireDayConfig != null && int.TryParse(passwordExpireDayConfig.ConfigValue, out int passwordExpireDay) && passwordExpireDay > 0)
            {
                if (pwdUpdateTime == null || DateTime.Now > pwdUpdateTime.Value.AddDays(passwordExpireDay))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
