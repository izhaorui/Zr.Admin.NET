using Infrastructure;
using Infrastructure.Attribute;
using Mapster;
using System.Collections;
using ZR.Common;
using ZR.Infrastructure.Constant;
using ZR.Infrastructure.Helper;
using ZR.Infrastructure.IPTools;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Model.System.Enums;
using ZR.Repository;
using ZR.ServiceCore.Security;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 系统用户
    /// </summary>
    [AppService(ServiceType = typeof(ISysUserService), ServiceLifetime = LifeTime.Transient)]
    public class SysUserService : BaseService<SysUser>, ISysUserService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly ISysRoleService RoleService;
        private readonly ISysUserRoleService UserRoleService;
        private readonly ISysUserPostService UserPostService;
        private readonly ISysUserMsgService UserMsgService;
        // 密码策略配置（sys.account.*）在主库 sys_config，必须经 ISysConfigService 读取，
        // 不能用 Queryable<SysConfig>()（会按 SysUser 的租户连接查错库）
        private readonly ISysConfigService SysConfigService;
        // 密码复杂度校验：注册/管理员重置/个人改密的规则必须同源，避免只在一处校验
        private readonly IPasswordPolicyService PasswordPolicyService;

        public SysUserService(
            ISysRoleService sysRoleService,
            ISysUserRoleService userRoleService,
            ISysUserPostService userPostService,
            ISysUserMsgService userMsgService,
            ISysConfigService sysConfigService,
            ISysDeptService deptService,
            IPasswordPolicyService passwordPolicyService)
        {
            RoleService = sysRoleService;
            UserRoleService = userRoleService;
            UserPostService = userPostService;
            UserMsgService = userMsgService;
            SysConfigService = sysConfigService;
            PasswordPolicyService = passwordPolicyService;
        }

        /// <summary>
        /// 根据条件分页查询用户列表
        /// </summary>
        /// <returns></returns>
        public PagedInfo<SysUserDto> SelectUserList(SysUserQueryDto user, PagerInfo pager)
        {
            var exp = Expressionable.Create<SysUser>();
            exp.AndIF(!string.IsNullOrEmpty(user.UserName), u => u.UserName.Contains(user.UserName));
            exp.AndIF(user.UserId > 0, u => u.UserId == user.UserId);
            exp.AndIF(user.Status != -1, u => u.Status == user.Status);
            exp.AndIF(user.BeginTime != DateTime.MinValue && user.BeginTime != null, u => u.Create_time >= user.BeginTime);
            exp.AndIF(user.EndTime != DateTime.MinValue && user.EndTime != null, u => u.Create_time <= user.EndTime);
            exp.AndIF(!user.Phonenumber.IsEmpty(), u => u.Phonenumber == user.Phonenumber);
            exp.And(u => u.DelFlag == DelFlagEnum.Normal);

            if (user.DeptId != 0)
            {
                var allChildDepts = Context.Queryable<SysDept>().ToChildList(it => it.ParentId, user.DeptId);

                exp.And(u => allChildDepts.Select(f => f.DeptId).ToList().Contains(u.DeptId));
            }
            var query = Queryable()
                .LeftJoin<SysDept>((u, dept) => u.DeptId == dept.DeptId)
                .LeftJoin<SysUser>((u, dept, leader) => u.LeaderId == leader.UserId)
                .Where(exp.ToExpression())
                .Select((u, dept, leader) => new SysUserDto
                {
                    UserId = u.UserId.SelectAll(),
                    DeptName = dept.DeptName,
                    LeaderName = leader.NickName,
                });
            var list = query.ToPage(pager);
            list.Result.MaskField(
                HttpContextExtension.HasSensitivePerm(App.HttpContext, SensitivePerms.ViewRealPhone),
                it => it.Phonenumber, (it, v) => it.Phonenumber = v, MaskUtil.MaskPhone);
            list.Result.MaskField(
                HttpContextExtension.HasSensitivePerm(App.HttpContext, SensitivePerms.ViewEmail),
                it => it.Email, (it, v) => it.Email = v, MaskUtil.MaskEmail);

            return list;
        }

        /// <summary>
        /// 通过用户ID查询用户
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public SysUserDto SelectUserById(long userId)
        {
            var userModel = Queryable()
                .Filter(null, true).WithCache(60 * 5)
                .Where(f => f.UserId == userId && f.DelFlag == DelFlagEnum.Normal)
                .First();
            var user = userModel.Adapt<SysUserDto>();
            if (user != null && user.UserId > 0)
            {
                if (user.LeaderId != null && user.LeaderId > 0)
                {
                    user.LeaderName = Queryable()
                        .Where(l => l.UserId == user.LeaderId)
                        .Select(l => l.NickName)
                        .First();
                }
                user.Roles = RoleService.SelectUserRoleListByUserId(userId);
                user.RoleIds = user.Roles.Select(x => x.RoleId).ToArray();

                user.MaskField(
                    HttpContextExtension.HasSensitivePerm(App.HttpContext, SensitivePerms.ViewRealPhone),
                    it => it.Phonenumber, (it, v) => it.Phonenumber = v, MaskUtil.MaskPhone);
                user.MaskField(
                    HttpContextExtension.HasSensitivePerm(App.HttpContext, SensitivePerms.ViewEmail),
                    it => it.Email, (it, v) => it.Email = v, MaskUtil.MaskEmail);
            }
            return user;
        }

        /// <summary>
        /// 校验用户名称是否唯一
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public string CheckUserNameUnique(string userName)
        {
            int count = Count(it => it.UserName == userName && it.DelFlag == DelFlagEnum.Normal);
            if (count > 0)
            {
                return UserConstants.NOT_UNIQUE;
            }
            return UserConstants.UNIQUE;
        }

        /// <summary>
        /// 校验手机号是否绑定
        /// </summary>
        /// <param name="phoneNum"></param>
        /// <returns></returns>
        public List<long> CheckPhoneBind(string phoneNum)
        {
            var list = GetList(it => it.Phonenumber == phoneNum);
            var temp = list.Select(x => x.UserId).ToList();
            return list.Count > 0 ? temp : [];
        }

        /// <summary>
        /// 绑定手机号
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="phoneNum"></param>
        /// <returns></returns>
        public int ChangePhoneNum(long userid, string phoneNum)
        {
            return Update(new SysUser() { Phonenumber = phoneNum }, it => new { it.Phonenumber }, f => f.UserId == userid);
        }

        /// <summary>
        /// 新增保存用户信息
        /// </summary>
        /// <param name="sysUser"></param>
        /// <returns></returns>
        public SysUser InsertUser(SysUser sysUser)
        {
            var result = UseTran(() =>
            {
                sysUser.UserId = Insertable(sysUser).ExecuteReturnIdentity();
                //新增用户角色信息
                UserRoleService.InsertUserRole(sysUser.RoleIds, sysUser.UserId);
                //新增用户岗位信息
                UserPostService.InsertUserPost(sysUser);
            });
            if (!result.IsSuccess)
            {
                throw new Exception("提交数据异常," + result.ErrorMessage, result.ErrorException);
            }
            UserMsgService.AddSysUserMsg(sysUser.UserId, "您的账号已创建，请使用初始密码登录后及时修改", UserMsgType.SYSTEM);
            return sysUser;
        }

        /// <summary>
        /// 修改用户信息
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public int UpdateUser(SysUserDto dto)
        {
            SysUser user = dto.Adapt<SysUser>();
            var roleIds = RoleService.SelectUserRoles(user.UserId);
            var diffArr = roleIds.Where(c => !((IList)dto.RoleIds).Contains(c)).ToArray();
            var diffArr2 = dto.RoleIds.Where(c => !((IList)roleIds).Contains(c)).ToArray();
            var roleChanged = diffArr.Length > 0 || diffArr2.Length > 0;
            var result = UseTran(() =>
            {
                if (roleChanged)
                {
                    //删除用户与角色关联
                    UserRoleService.DeleteUserRoleByUserId((int)user.UserId);
                    //新增用户与角色关联
                    UserRoleService.InsertUserRole(dto.RoleIds, dto.UserId);
                }
                // 删除用户与岗位关联
                UserPostService.Delete(user.UserId);
                // 新增用户与岗位管理
                UserPostService.InsertUserPost(user);
                ChangeUser(user);
                UserMsgService.AddSysUserMsg(user.UserId, "你的资料已被修改", UserMsgType.SYSTEM);
                if (roleChanged)
                {
                    UserMsgService.AddSysUserMsg(user.UserId, "您的角色/权限已变更，如有疑问请联系管理员", UserMsgType.SYSTEM);
                }
            });
            return result.IsSuccess ? 1 : 0;
        }

        public int ChangeUser(SysUser user)
        {
            user.Update_time = DateTime.Now;
            var result = Update(user, t => new
            {
                t.NickName,
                t.Email,
                t.Phonenumber,
                t.DeptId,
                t.LeaderId,
                t.Status,
                t.Sex,
                t.PostIds,
                t.Remark,
                t.Update_by,
                t.Update_time
            }, true);

            // 编辑用户接口同样会写 Status：与 changeStatus 保持一致，让状态复核缓存立即生效，
            // 否则"编辑里勾选停用"要等缓存自然过期才踢得掉人。
            if (result > 0 && user.UserId > 0)
            {
                if (user.Status == 1)
                {
                    CacheService.RemoveUserSession(user.UserId);
                    CacheService.SetUserStatus(user.UserId, 1);
                    CacheService.RemoveUserPerms(GlobalConstant.UserPermKEY + user.UserId);
                }
                else
                {
                    CacheService.RemoveUserStatus(user.UserId);
                }
            }
            return result;
        }

        /// <summary>
        /// 重置密码
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="password"></param>
        /// <param name="notifyContent">重置后通知用户的内容，为空则不发送通知</param>
        /// <returns></returns>
        public int ResetPwd(long userid, string password, string notifyContent = null)
        {
            var result = Update(new SysUser()
            {
                UserId = userid,
                Password = password,
                PwdUpdateTime = DateTime.Now
            }, it => new { it.Password, it.PwdUpdateTime }, f => f.UserId == userid);
            if (result > 0 && !string.IsNullOrEmpty(notifyContent))
            {
                UserMsgService.AddSysUserMsg(userid, notifyContent, UserMsgType.SYSTEM);
            }
            return result;
        }

        /// <summary>
        /// 是否为初始（默认）密码：开启 sys.account.initPasswordModify 且从未改过密码。
        /// </summary>
        /// <param name="pwdUpdateTime">用户最后密码更新时间，null 表示从未修改</param>
        /// <returns></returns>
        public bool IsInitPassword(DateTime? pwdUpdateTime)
        {
            var initPasswordModify = SysConfigService.GetSysConfigByKey("sys.account.initPasswordModify");
            return initPasswordModify != null && initPasswordModify.ConfigValue == "1" && pwdUpdateTime == null;
        }

        /// <summary>
        /// 密码是否已过期：按 sys.account.passwordExpireDay 判定，未配置或配置 &lt;=0 时永不过期。
        /// </summary>
        /// <param name="pwdUpdateTime">用户最后密码更新时间，null 视为已过期</param>
        /// <returns></returns>
        public bool IsPasswordExpired(DateTime? pwdUpdateTime)
        {
            var passwordExpireDayConfig = SysConfigService.GetSysConfigByKey("sys.account.passwordExpireDay");
            if (passwordExpireDayConfig != null && int.TryParse(passwordExpireDayConfig.ConfigValue, out int passwordExpireDay) && passwordExpireDay > 0)
            {
                if (pwdUpdateTime == null || DateTime.Now > pwdUpdateTime.Value.AddDays(passwordExpireDay))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 修改用户状态
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public int ChangeUserStatus(SysUser user)
        {
            CheckUserAllowed(user);
            var result = Update(user, it => new { it.Status }, f => f.UserId == user.UserId);
            if (result > 0)
            {
                if (user.Status == 1)
                {
                    // 禁用即时生效：清会话只是"挤下线"（单设备登录开启时才拦得住），
                    // 真正的兜底是写入不可用状态，由 JwtAuthMiddleware 在每次请求复核后拒绝存量 token。
                    CacheService.RemoveUserSession(user.UserId);
                    CacheService.SetUserStatus(user.UserId, 1);
                    CacheService.RemoveUserPerms(GlobalConstant.UserPermKEY + user.UserId);
                    UserMsgService.AddSysUserMsg(user.UserId, "您的账号已被管理员禁用", UserMsgType.SYSTEM);
                }
                else if (user.Status == 0)
                {
                    // 清除缓存后由下一次请求复核重新回填，避免"启用后仍被缓存判为禁用"
                    CacheService.RemoveUserStatus(user.UserId);
                    UserMsgService.AddSysUserMsg(user.UserId, "您的账号已恢复启用", UserMsgType.SYSTEM);
                }
            }
            return result;
        }

        /// <summary>
        /// 后台用户是否可用（未禁用且未删除）。
        ///
        /// JWT 无状态：仅靠登录时校验 Status 挡不住"先登录后被禁用，存量令牌在有效期内仍可调用"，
        /// 且中间件会自动续期，等于永久有效。本方法供中间件做状态复核。
        /// 状态走 60 秒缓存（Redis/内存由 CacheStore 按配置自动切换），禁用时主动清除，
        /// 多实例下最迟 60 秒全端生效；缓存未命中时回查一次库。
        /// </summary>
        public bool IsUserActive(long userId)
        {
            if (userId <= 0)
            {
                return false;
            }

            try
            {
                var cached = CacheService.GetUserStatus(userId);
                if (cached.HasValue)
                {
                    return cached.Value == 0;
                }

                // ClearFilter：状态复核只关心"这人还能不能登录"，不应受数据权限/租户过滤影响而查不到
                var user = Context.Queryable<SysUser>().ClearFilter()
                    .Where(it => it.UserId == userId)
                    .Select(it => new { it.Status, it.DelFlag })
                    .First();
                var status = user == null || user.Status == 1 || user.DelFlag == DelFlagEnum.Deleted ? 1 : 0;
                CacheService.SetUserStatus(userId, status);
                return status == 0;
            }
            catch (Exception ex)
            {
                // 查询/缓存异常时放行：业务层查询同样依赖 DB 与缓存，它们不可用时不构成额外安全增量，
                // 但若这里直接拒绝，DB/Redis 抖动会导致全部在线用户被踢出。
                logger.Warn(ex, $"用户状态复核失败，本次放行：UserId={userId}");
                return true;
            }
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public int DeleteUser(long userid)
        {
            CheckUserAllowed(new SysUser() { UserId = userid });
            var result = UseTran(() =>
            {
                //删除用户与角色关联
                UserRoleService.DeleteUserRoleByUserId((int)userid);
                // 删除用户与岗位关联
                UserPostService.Delete(userid);
                Update(new SysUser() { UserId = userid, DelFlag = DelFlagEnum.Deleted }, it => new { it.DelFlag }, f => f.UserId == userid);
            });
            if (result.IsSuccess)
            {
                // 删除同样要让存量 token 立即失效，否则被删用户仍可在令牌有效期内调用接口
                CacheService.RemoveUserSession(userid);
                CacheService.SetUserStatus(userid, 1);
                CacheService.RemoveUserPerms(GlobalConstant.UserPermKEY + userid);
            }
            return result.IsSuccess ? 1 : 0;
        }

        /// <summary>
        /// 修改用户头像
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public int UpdatePhoto(SysUser user)
        {
            return Update(user, it => new { it.Avatar }, f => f.UserId == user.UserId); ;
        }

        /// <summary>
        /// 注册用户
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public SysUser Register(RegisterDto dto)
        {
            // 密码复杂度统一由策略服务判定（等保：鉴别信息具备复杂度要求）。
            // 旧实现 Tools.PasswordStrength 只挡纯数字/纯符号/纯字母，任何 6 位混合口令都能通过。
            PasswordPolicyService.ValidatePassword(dto.Password, dto.Username);
            if (!Tools.CheckUserName(dto.Username))
            {
                throw new CustomException("用户名不符合要求");
            }
            //密码md5
            string password = SecurityHelper.Md5(dto.Password);
            var ip_info = IpTool.Search(dto.UserIP);
            SysUser user = new()
            {
                Create_time = DateTime.Now,
                UserName = dto.Username,
                NickName = dto.Username,
                Password = password,
                Status = 0,
                DeptId = 0,
                Remark = "用户注册",
                Province = ip_info.Province,
                City = ip_info.City
            };
            if (UserConstants.NOT_UNIQUE.Equals(CheckUserNameUnique(dto.Username)))
            {
                throw new CustomException($"保存用户{dto.Username}失败，注册账号已存在");
            }
            user.UserId = Insertable(user).ExecuteReturnIdentity();
            return user;
        }

        /// <summary>
        /// 校验角色是否允许操作
        /// </summary>
        /// <param name="user"></param>
        public void CheckUserAllowed(SysUser user)
        {
            if (user.IsAdmin)
            {
                throw new CustomException("不允许操作超级管理员角色");
            }
        }

        /// <summary>
        /// 校验用户是否有数据权限
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="loginUserId"></param>
        public void CheckUserDataScope(long userid, long loginUserId)
        {
        }

        /// <summary>
        /// 导入数据
        /// </summary>
        /// <param name="users"></param>
        /// <returns></returns>
        public (string, object, object) ImportUsers(List<SysUser> users)
        {
            users.ForEach(x =>
            {
                x.Create_time = DateTime.Now;
                x.Status = 0;
                x.DelFlag = DelFlagEnum.Normal;
                x.Password = "E10ADC3949BA59ABBE56E057F20F883E";
                x.Remark = x.Remark.IsEmpty() ? "数据导入" : x.Remark;
            });
            var x = Context.Storageable(users)
                .SplitInsert(it => !it.Any())
                .SplitIgnore(it => it.Item.UserName == GlobalConstant.AdminRole)
                .SplitError(x => x.Item.UserName.IsEmpty(), "用户名不能为空")
                .SplitError(x => !Tools.CheckUserName(x.Item.UserName), "用户名不符合规范")
                .WhereColumns(it => it.UserName)//如果不是主键可以这样实现（多字段it=>new{it.x1,it.x2}）
                .ToStorage();
            var result = x.AsInsertable.ExecuteCommand();//插入可插入部分;

            string msg = string.Format(" 插入{0} 更新{1} 错误数据{2} 不计算数据{3} 删除数据{4} 总共{5}",
                               x.InsertList.Count,
                               x.UpdateList.Count,
                               x.ErrorList.Count,
                               x.IgnoreList.Count,
                               x.DeleteList.Count,
                               x.TotalList.Count);
            //输出统计                      
            Console.WriteLine(msg);

            //输出错误信息               
            foreach (var item in x.ErrorList)
            {
                Console.WriteLine("userName为" + item.Item.UserName + " : " + item.StorageMessage);
            }
            foreach (var item in x.IgnoreList)
            {
                Console.WriteLine("userName为" + item.Item.UserName + " : " + item.StorageMessage);
            }

            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="user">登录实体</param>
        /// <returns></returns>
        public SysUser Login(LoginBodyDto user)
        {
            return Context.Queryable<SysUser>()
                .Where(it => it.UserName == user.Username && it.Password.ToLower() == user.Password.ToLower() && it.DelFlag == DelFlagEnum.Normal)
                .First();
        }

        /// <summary>
        /// 修改登录信息
        /// </summary>
        /// <param name="userIP"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public void UpdateLoginInfo(string userIP, long userId)
        {
            Update(new SysUser() { LoginIP = userIP, LoginDate = DateTime.Now, UserId = userId }, it => new { it.LoginIP, it.LoginDate });
        }

        /// <summary>
        /// 保存双因子认证状态。
        /// 用 SetColumns(prop == value) 逐列赋值而非 SetColumns(new T{...})：
        /// 后者在部分 SqlSugar 版本对 null 成员会跳过，导致"解绑后密钥残留"。
        /// </summary>
        public int SaveTwoFactor(long userId, int enabled, string secret, DateTime? boundAt, string recoveryCodesJson)
        {
            return Context.Updateable<SysUser>()
                .SetColumns(it => it.TwoFactorEnabled == enabled)
                .SetColumns(it => it.TwoFactorSecret == secret)
                .SetColumns(it => it.TwoFactorBoundAt == boundAt)
                .SetColumns(it => it.TwoFactorRecoveryCodes == recoveryCodesJson)
                .Where(it => it.UserId == userId)
                .ExecuteCommand();
        }

        /// <summary>
        /// 清空双因子认证配置。统一写空串而不是 NULL：
        /// 服务层把"空串"与"未绑定"视为等价，既避开 null 赋值歧义，也让存量列无需区分 NULL/''。
        /// </summary>
        public int ClearTwoFactor(long userId)
        {
            return Context.Updateable<SysUser>()
                .SetColumns(it => it.TwoFactorEnabled == 0)
                .SetColumns(it => it.TwoFactorSecret == string.Empty)
                .SetColumns(it => it.TwoFactorBoundAt == null)
                .SetColumns(it => it.TwoFactorRecoveryCodes == string.Empty)
                .Where(it => it.UserId == userId)
                .ExecuteCommand();
        }
    }
}
