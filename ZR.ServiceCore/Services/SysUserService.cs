using Infrastructure;
using Infrastructure.Attribute;
using System.Collections;
using ZR.Common;
using ZR.Infrastructure.IPTools;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Repository;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 系统用户
    /// </summary>
    [AppService(ServiceType = typeof(ISysUserService), ServiceLifetime = LifeTime.Transient)]
    public class SysUserService : BaseService<SysUser>, ISysUserService
    {
        private readonly ISysRoleService RoleService;
        private readonly ISysUserRoleService UserRoleService;
        private readonly ISysUserPostService UserPostService;
        private readonly ISysUserMsgService UserMsgService;

        public SysUserService(
            ISysRoleService sysRoleService,
            ISysUserRoleService userRoleService,
            ISysUserPostService userPostService,
            ISysUserMsgService userMsgService)
        {
            RoleService = sysRoleService;
            UserRoleService = userRoleService;
            UserPostService = userPostService;
            UserMsgService = userMsgService;
        }

        /// <summary>
        /// 根据条件分页查询用户列表
        /// </summary>
        /// <returns></returns>
        public PagedInfo<SysUser> SelectUserList(SysUserQueryDto user, PagerInfo pager)
        {
            var exp = Expressionable.Create<SysUser>();
            exp.AndIF(!string.IsNullOrEmpty(user.UserName), u => u.UserName.Contains(user.UserName));
            exp.AndIF(user.UserId > 0, u => u.UserId == user.UserId);
            exp.AndIF(user.Status != -1, u => u.Status == user.Status);
            exp.AndIF(user.BeginTime != DateTime.MinValue && user.BeginTime != null, u => u.Create_time >= user.BeginTime);
            exp.AndIF(user.EndTime != DateTime.MinValue && user.EndTime != null, u => u.Create_time <= user.EndTime);
            exp.AndIF(!user.Phonenumber.IsEmpty(), u => u.Phonenumber == user.Phonenumber);
            exp.And(u => u.DelFlag == 0);

            if (user.DeptId != 0)
            {
                var allChildDepts = Context.Queryable<SysDept>().ToChildList(it => it.ParentId, user.DeptId);

                exp.And(u => allChildDepts.Select(f => f.DeptId).ToList().Contains(u.DeptId));
            }
            var query = Queryable()
                .LeftJoin<SysDept>((u, dept) => u.DeptId == dept.DeptId)
                .Where(exp.ToExpression())
                .Select((u, dept) => new SysUser
                {
                    UserId = u.UserId.SelectAll(),
                    DeptName = dept.DeptName,
                });

            return query.ToPage(pager);
        }

        /// <summary>
        /// 通过用户ID查询用户
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public SysUser SelectUserById(long userId)
        {
            var user = Queryable().Filter(null, true).WithCache(60 * 5)
                .Where(f => f.UserId == userId && f.DelFlag == 0).First();
            if (user != null && user.UserId > 0)
            {
                user.Roles = RoleService.SelectUserRoleListByUserId(userId);
                user.RoleIds = user.Roles.Select(x => x.RoleId).ToArray();
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
            int count = Count(it => it.UserName == userName && it.DelFlag == 0);
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
            return list.Count > 0 ?  temp : new List<long>();
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
                UserRoleService.InsertUserRole(sysUser);
                //新增用户岗位信息
                UserPostService.InsertUserPost(sysUser);
            });
            if (!result.IsSuccess)
            {
                throw new Exception("提交数据异常," + result.ErrorMessage, result.ErrorException);
            }
            return sysUser;
        }

        /// <summary>
        /// 修改用户信息
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public int UpdateUser(SysUser user)
        {
            var roleIds = RoleService.SelectUserRoles(user.UserId);
            var diffArr = roleIds.Where(c => !((IList)user.RoleIds).Contains(c)).ToArray();
            var diffArr2 = user.RoleIds.Where(c => !((IList)roleIds).Contains(c)).ToArray();
            var result = UseTran(() =>
            {
                if (diffArr.Length > 0 || diffArr2.Length > 0)
                {
                    //删除用户与角色关联
                    UserRoleService.DeleteUserRoleByUserId((int)user.UserId);
                    //新增用户与角色关联
                    UserRoleService.InsertUserRole(user);
                }
                // 删除用户与岗位关联
                UserPostService.Delete(user.UserId);
                // 新增用户与岗位管理
                UserPostService.InsertUserPost(user);
                ChangeUser(user);
                UserMsgService.AddSysUserMsg(user.UserId, "你的资料已被修改", UserMsgType.SYSTEM);
            });
            return result.IsSuccess ? 1 : 0;
        }

        public int ChangeUser(SysUser user)
        {
            user.Update_time = DateTime.Now;
            return Update(user, t => new
            {
                t.NickName,
                t.Email,
                t.Phonenumber,
                t.DeptId,
                t.Status,
                t.Sex,
                t.PostIds,
                t.Remark,
                t.Update_by,
                t.Update_time
            }, true);
        }

        /// <summary>
        /// 重置密码
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public int ResetPwd(long userid, string password)
        {
            return Update(new SysUser() { UserId = userid, Password = password }, it => new { it.Password }, f => f.UserId == userid);
        }

        /// <summary>
        /// 修改用户状态
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public int ChangeUserStatus(SysUser user)
        {
            CheckUserAllowed(user);
            return Update(user, it => new { it.Status }, f => f.UserId == user.UserId);
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
                Update(new SysUser() { UserId = userid, DelFlag = 2 }, it => new { it.DelFlag }, f => f.UserId == userid);
            });
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
            if (!Tools.PasswordStrength(dto.Password))
            {
                throw new CustomException("密码强度不符合要求");
            }
            if (!Tools.CheckUserName(dto.Username))
            {
                throw new CustomException("用户名不符合要求");
            }
            //密码md5
            string password = NETCore.Encrypt.EncryptProvider.Md5(dto.Password);
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
                x.DelFlag = 0;
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
            return GetFirst(it => it.UserName == user.Username && it.Password.ToLower() == user.Password.ToLower() && it.DelFlag == 0);
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
    }
}
