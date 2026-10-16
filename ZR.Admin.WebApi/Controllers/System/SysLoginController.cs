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
        private readonly ISysTwoFactorService twoFactorService;
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

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
            ISysDeptService sysDeptService,
            ISysTwoFactorService twoFactorService)
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
            this.twoFactorService = twoFactorService;
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
            var user = sysLoginService.Login(loginBody, new SysLogininfor() { LoginLocation = location }, out bool needTwoFactor);

            // 双因子闸门：只下发一次性票据，token 延迟到 /login/2fa/verify 校验通过后才签发
            if (needTwoFactor)
            {
                return ToResponse(BuildTwoFactorTicket(user, loginBody.TenantId, loginBody.ClientId,
                    loginBody.LoginIP, location, TwoFactorLoginSource.Account));
            }

            string abnormalNotice = sysLoginService.GetAbnormalLoginNotice(user, loginBody.LoginIP);
            return IssueToken(user, loginBody.TenantId, loginBody.ClientId, loginBody.LoginIP, abnormalNotice: abnormalNotice);
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
            CacheService.RemoveUserSession(userid);
            return SUCCESS(new { name, id = userid });
        }

        #region 双因子登录（TOTP）

        /// <summary>
        /// 双因子登录第二步：校验 TOTP（或一次性恢复码）后签发 token。
        ///
        /// 票据一次性且短时有效，避免"密码已知但缺验证码"的状态长期挂起。
        /// 本接口允许匿名访问，安全性完全依赖：票据随机性 + 一次性消费 + 失败锁定。
        /// </summary>
        [HttpPost("login/2fa/verify")]
        [AllowAnonymous]
        // 请求含票据与动态码/恢复码、响应含 token，两者都属凭据，一律不落操作日志
        [Log(Title = "双因子验证", IsSaveRequestData = false, IsSaveResponseData = false)]
        public IActionResult VerifyTwoFactor([FromBody] TwoFactorVerifyDto dto)
        {
            if (dto == null || dto.Ticket.IsEmpty())
            {
                throw new CustomException("请求参数错误");
            }

            var settings = AppSettings.Bind("TwoFactor", new TwoFactorSettings());
            var ticketMinutes = settings.TicketMinutes > 0 ? settings.TicketMinutes : 5;

            // 租户由票据载荷携带：先消费票据再切租户上下文，因此不依赖前端重复提交 tenantId
            var payload = CacheService.ConsumeTwoFactorTicket<TwoFactorTicketPayload>(dto.Ticket, ticketMinutes);
            if (payload == null)
            {
                return ToResponse(ResultCode.LOGIN_ERROR, "双因子验证已超时，请重新登录");
            }

            using var tenantScope = TenantContext.Change(payload.TenantId);

            var user = twoFactorService.GetUserForAuth(payload.UserId);
            if (user == null || user.Status == 1)
            {
                return ToResponse(ResultCode.LOGIN_ERROR, "账号不可用");
            }

            var clientInfo = HttpContext.GetClientInfo();
            SysLogininfor logininfor = new()
            {
                UserName = user.UserName,
                UserId = user.UserId,
                Ipaddr = payload.LoginIP,
                ClientId = payload.ClientId,
                LoginTime = DateTime.Now,
                LoginLocation = payload.LoginLocation,
                Browser = clientInfo.ToString(),
                Os = clientInfo.OS.ToString(),
                Status = "1",
                Msg = "双因子验证失败"
            };

            try
            {
                twoFactorService.VerifyCodeOrThrow(user, dto.Code);
            }
            catch (Exception)
            {
                // 2FA 环节的失败是重要安全信号，必须与普通登录失败一样落库留痕后再抛出
                sysLoginService.AddLoginInfo(logininfor, payload.TenantId);
                throw;
            }

            // 设备登记与"记住此设备"已合并为一个动作，在 IssueToken 里统一完成（三个登录入口共用），
            // 这里只负责把"是否记住"与本次设备标识传下去。
            var clientId = dto.ClientId.IsEmpty() ? payload.ClientId : dto.ClientId;

            // 与普通登录同一口径：先取异地提醒（用的是本次之前的登录 IP），再更新最后登录信息
            string abnormalNotice = sysLoginService.GetAbnormalLoginNotice(user, payload.LoginIP);

            logininfor.Status = "0";
            logininfor.Msg = "登录成功";
            sysLoginService.AddLoginInfo(logininfor, payload.TenantId);
            sysUserService.UpdateLoginInfo(payload.LoginIP, user.UserId);

            return IssueToken(user, payload.TenantId, clientId, payload.LoginIP, dto.RememberDevice, abnormalNotice);
        }

        /// <summary>
        /// 组装"需要双因子验证"的响应：不含 token，只给一次性票据。
        /// 票据里带租户与设备标识，第二步据此查用户、判断是否信任设备。
        /// </summary>
        private ApiResult BuildTwoFactorTicket(SysUser user, string tenantId, string clientId, string loginIp,
            string location, TwoFactorLoginSource source)
        {
            var settings = AppSettings.Bind("TwoFactor", new TwoFactorSettings());
            var minutes = settings.TicketMinutes > 0 ? settings.TicketMinutes : 5;
            var ticket = Guid.NewGuid().ToString("N");

            CacheService.SetTwoFactorTicket(ticket, new TwoFactorTicketPayload
            {
                UserId = user.UserId,
                UserName = user.UserName,
                NickName = user.NickName,
                TenantId = tenantId,
                ClientId = clientId,
                LoginIP = loginIp,
                LoginLocation = location,
                LoginSource = (int)source
            }, minutes);

            // 刻意不用 200：此时登录尚未完成（未签发 token），
            // 若返回 200，前端/移动端会把它当成登录成功去写 token，最终拿到 undefined 再请求接口报 401。
            var result = new ApiResult((int)ResultCode.MFA_REQUIRED, "需要双因子验证");
            result.Put("mfaRequired", true);
            result.Put("ticket", ticket);
            result.Put("expireMinutes", minutes);
            return result;
        }

        /// <summary>
        /// 签发 token 的统一出口。
        ///
        /// 账号密码登录、手机号登录、双因子第二步都从这里签发，
        /// 保证权限装配、单会话、"本会话已通过双因子"标记、**登录设备登记**四件事
        /// 不会在某个入口漏掉。
        /// </summary>
        /// <param name="clientId">设备标识（前端指纹）；为空表示无法识别设备</param>
        /// <param name="trustDevice">是否把该设备标记为受信任（用户勾选"记住此设备"）</param>
        private IActionResult IssueToken(SysUser user, string tenantId, string clientId, string loginIp,
            bool trustDevice = false, string? abnormalNotice = null)
        {
            List<SysRole> roles = roleService.SelectUserRoleListByUserId(user.UserId);
            //权限集合 eg *:*:*,system:user:list（按套餐菜单过滤）
            List<string> permissions = permissionService.GetMenuPermission(new SysUserDto() { UserId = user.UserId });

            LoginUser loginUser = new(user.Adapt<LoginUser>(), roles.Adapt<List<Roles>>())
            {
                TenantId = tenantId,
                Permissions = permissions,
                ScopeType = SysLoginService.ComputeScopeType(roles),
                SessionId = Guid.NewGuid().ToString("N")// 单设备登录：每次登录生成新会话ID并缓存，旧会话将被中间件判定失效
            };

            // 设备登记放在这个统一出口：三个登录入口都会经过这里，
            // 避免"某个入口忘了登记"导致设备列表与新设备提醒出现盲区。
            // 登记失败不影响登录 —— 它不是安全闸门，只承担审计与提醒职责。
            try
            {
                twoFactorService.RecordLoginDevice(user.UserId, clientId,
                    HttpContext.GetClientInfo().ToString(), loginIp, trustDevice);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"登录设备登记失败：UserId={user.UserId}");
            }

            CacheService.SetUserSession(loginUser.UserId, loginUser.SessionId);
            // 能走到签发说明身份校验已完整（未启用 2FA，或刚通过 2FA / 命中可信设备）。
            // 标记该会话供扫码登录判定"移动端当前会话确实过了 2FA"。
            CacheService.SetTwoFactorSessionOk(loginUser.UserId, loginUser.SessionId);

            var token = JwtUtil.GenerateJwtToken(JwtUtil.AddClaims(loginUser));
            ApiResult apiResult = new((int)ResultCode.SUCCESS, "success", token);
            if (!string.IsNullOrEmpty(abnormalNotice))
            {
                apiResult.Put("notice", abnormalNotice);
            }

            return ToResponse(apiResult);
        }
        #endregion

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
            CacheService.SetDataScopeDeptIds(userId, deptService.ComputeDataScopeDeptIds(userRoles, user.DeptId));

            // 登录后全局品牌信息（顶栏/侧边栏展示租户Logo等），无需额外请求
            object tenant = null;
            if (App.IsTenantEnabled())
            {
                var tid = App.GetCurrentTenantId();
                if (!string.IsNullOrWhiteSpace(tid))
                {
                    var t = sysTenantService.GetByTenantId(tid);
                    if (t != null)
                    {
                        tenant = new { t.TenantId, t.TenantName, t.Domain, t.Logo };
                    }
                }
            }

            return SUCCESS(new
            {
                user = user.Adapt<SysUserDto>(),
                roles,
                permissions,
                isDefaultModifyPwd = sysUserService.IsInitPassword(user.PwdUpdateTime),
                isPasswordExpired = sysUserService.IsPasswordExpired(user.PwdUpdateTime),
                tenant
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

            var menus = sysMenuService.GetAppMenus(perms, v);

            // 非主租户过滤平台专属菜单（monitor:* 等，见 TenantSettings:PlatformMenuPermPrefixes），
            // 与 PC 端 /getRouters 保持一致，避免子租户拿到入口但点击/请求被 403「当前租户不允许访问平台级能力」。
            if (App.IsTenantEnabled())
            {
                var isMainTenant = string.Equals(App.GetCurrentTenantId(), App.MainDbConfigId, StringComparison.OrdinalIgnoreCase);
                menus = TenantFeaturePolicy.FilterPlatformMenusForNonMainTenant(menus, isMainTenant);
            }

            return SUCCESS(menus);
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

            // 登录页品牌化：按访问子域名解析出的当前租户（TenantResolveMiddleware 已写入 TenantContext.CurrentTenantId）。
            // 用于登录页直接展示对应租户 Logo，无需登录即可获取。
            object currentTenant = null;
            if (useTenant)
            {
                var currentTenantId = App.GetCurrentTenantId();
                if (!string.IsNullOrWhiteSpace(currentTenantId))
                {
                    var t = sysTenantService.GetByTenantId(currentTenantId);
                    if (t != null)
                    {
                        currentTenant = new { t.TenantId, t.TenantName, t.Domain, t.Logo };
                    }
                }
            }

            return SUCCESS(new { useTenant, tenants, currentTenant });
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
            sysTenantService.CheckTenant(dto.TenantId);
            using var tenantScope = TenantContext.Change(dto.TenantId);
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
        [HttpGet("/generateQrcode")]
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
        [HttpPost("/verifyScan")]
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
        [HttpPost("/scanLogin")]
        [Log(Title = "扫码登录")]
        public IActionResult ScanLogin([FromBody] ScanDto dto)
        {
            if (dto == null) { return ToResponse(ResultCode.CUSTOM_ERROR, "扫码失败"); }
            var name = App.HttpContext.GetName();

            sysLoginService.CheckLockUser(name);

            LoginUser tokenModel = JwtUtil.GetLoginUser(HttpContext);
            if (CacheService.GetScanLogin(dto.Uuid) is not null)
            {
                // 双因子闸门：已启用 2FA 的用户，移动端"当前会话"必须带 2FA 已通过标记。
                // 移动端登录本身会强制走 2FA 并打标记；拿着启用 2FA 之前签发的旧 token 扫码会被挡下，
                // 否则扫码就成了绕过 2FA 的旁路。
                if (!CacheService.IsTwoFactorSessionOk(tokenModel.UserId, tokenModel.SessionId)
                    && twoFactorService.IsEnabled(tokenModel.UserId))
                {
                    throw new CustomException(ResultCode.LOGIN_ERROR, "该账号已启用双因子认证，请在移动端重新登录后再扫码", false);
                }

                // 单设备登录：扫码登录视为一次新登录，生成新会话ID踢掉旧设备
                tokenModel.SessionId = Guid.NewGuid().ToString("N");
                CacheService.SetUserSession(tokenModel.UserId, tokenModel.SessionId);
                CacheService.SetTwoFactorSessionOk(tokenModel.UserId, tokenModel.SessionId);
                Dictionary<string, object> dict = new()
                {
                    { "status", "success" },
                    { "token", JwtUtil.GenerateJwtToken(JwtUtil.AddClaims(tokenModel)) }
                };
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
            sysLoginService.PhoneLogin(loginBody, new SysLogininfor() { LoginLocation = location }, infoModel, out bool needTwoFactor);

            // 双因子闸门：与账号密码登录共用一个出口，避免手机号登录成为绕过 2FA 的旁路
            if (needTwoFactor)
            {
                return ToResponse(BuildTwoFactorTicket(info, loginBody.TenantId, loginBody.ClientId,
                    loginBody.LoginIP, location, TwoFactorLoginSource.Phone));
            }

            return IssueToken(info, loginBody.TenantId, loginBody.ClientId, loginBody.LoginIP);
        }

        /// <summary>
        /// 手机号绑定
        /// </summary>
        /// <param name="loginBody"></param>
        /// <returns></returns>
        [Route("/phoneBind")]
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
    }
}
