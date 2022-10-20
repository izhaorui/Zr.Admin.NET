using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Extensions;
using SqlSugar;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ZR.Common;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Repository;
using ZR.Service.System.IService;

namespace ZR.Service
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

        public SysUserService(
            ISysRoleService sysRoleService,
            ISysUserRoleService userRoleService,
            ISysUserPostService userPostService)
        {
            RoleService = sysRoleService;
            UserRoleService = userRoleService;
            UserPostService = userPostService;
        }

        /// <summary>
        /// 根据条件分页查询用户列表
        /// </summary>
        /// <returns></returns>
        public PagedInfo<SysUser> SelectUserList(SysUser user, PagerInfo pager)
        {
            var exp = Expressionable.Create<SysUser>();
            exp.AndIF(!string.IsNullOrEmpty(user.UserName), u => u.UserName.Contains(user.UserName));
            exp.AndIF(!string.IsNullOrEmpty(user.Status), u => u.Status == user.Status);
            exp.AndIF(user.BeginTime != DateTime.MinValue && user.BeginTime != null, u => u.Create_time >= user.BeginTime);
            exp.AndIF(user.EndTime != DateTime.MinValue && user.EndTime != null, u => u.Create_time <= user.EndTime);
            exp.AndIF(!user.Phonenumber.IsEmpty(), u => u.Phonenumber == user.Phonenumber);
            exp.And(u => u.DelFlag == "0");

            if (user.DeptId != 0)
            {
                List<SysDept> depts = Context.Queryable<SysDept>().ToList();

                var newDepts = depts.FindAll(delegate (SysDept dept)
                {
                    string[] parentDeptId = dept.Ancestors.Split(",", StringSplitOptions.RemoveEmptyEntries);
                    return parentDeptId.Contains(user.DeptId.ToString());
                });
                string[] deptArr = newDepts.Select(x => x.DeptId.ToString()).ToArray();
                exp.AndIF(user.DeptId != 0, u => u.DeptId == user.DeptId || deptArr.Contains(u.DeptId.ToString()));
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
            var user = Queryable().Filter(null, true).Where(f => f.UserId == userId).First();
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
            int count = Count(it => it.UserName == userName);
            if (count > 0)
            {
                return UserConstants.NOT_UNIQUE;
            }
            return UserConstants.UNIQUE;
        }

        /// <summary>
        /// 新增保存用户信息
        /// </summary>
        /// <param name="sysUser"></param>
        /// <returns></returns>
        public long InsertUser(SysUser sysUser)
        {
            sysUser.Create_time = DateTime.Now;
            long userId = Insertable(sysUser).ExecuteReturnIdentity();
            sysUser.UserId = userId;
            //新增用户角色信息
            UserRoleService.InsertUserRole(sysUser);
            //新增用户岗位信息
            UserPostService.InsertUserPost(sysUser);
            return userId;
        }

        /// <summary>
        /// 修改用户信息
        /// </summary>
        /// <param name="sysUser"></param>
        /// <returns></returns>
        public int UpdateUser(SysUser user)
        {
            var roleIds = RoleService.SelectUserRoles(user.UserId);
            var diffArr = roleIds.Where(c => !((IList)user.RoleIds).Contains(c)).ToArray();
            var diffArr2 = user.RoleIds.Where(c => !((IList)roleIds).Contains(c)).ToArray();

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
            return ChangeUser(user);
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
            return Update(user, it => new { it.Status }, f => f.UserId == user.UserId);
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public int DeleteUser(long userid)
        {
            CheckUserAllowed(new SysUser() { UserId = userid});
            //删除用户与角色关联
            UserRoleService.DeleteUserRoleByUserId((int)userid);
            // 删除用户与岗位关联
            UserPostService.Delete(userid);
            return Update(new SysUser() { UserId = userid, DelFlag = "2" }, it => new { it.DelFlag }, f => f.UserId == userid);
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
            //密码md5
            string password = NETCore.Encrypt.EncryptProvider.Md5(dto.Password);
            if (!Tools.PasswordStrength(dto.Password))
            {
                throw new CustomException("密码强度不符合要求");
            }
            SysUser user = new()
            {
                Create_time = DateTime.Now,
                UserName = dto.Username,
                NickName = dto.Username,
                Password = password,
                Status = "0",
                DeptId = 0,
                Remark = "用户注册"
            };

            user.UserId = Insertable(user).ExecuteReturnIdentity();
            return user;
        }

        /// <summary>
        /// 校验角色是否允许操作
        /// </summary>
        /// <param name="user"></param>
        public void CheckUserAllowed(SysUser user)
        {
            if (user.IsAdmin())
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
            if (!SysUser.IsAdmin(loginUserId))
            {
                SysUser user = new SysUser() { UserId = userid};
                
                //TODO 判断用户是否有数据权限
            }
        }

        /// <summary>
        /// 导入数据
        /// </summary>
        /// <param name="users"></param>
        /// <returns></returns>
        public string ImportUsers(List<SysUser> users)
        {
            users.ForEach(x =>
            {
                x.Create_time = DateTime.Now;
                x.Status = "0";
                x.DelFlag = "0";
                x.Password = "E10ADC3949BA59ABBE56E057F20F883E";
                x.Remark = "数据导入";
            });
            var x = Context.Storageable(users)
                .SplitInsert(it => !it.Any())
                .SplitIgnore(it => it.Item.UserName == GlobalConstant.AdminRole)
                .SplitError(x => x.Item.UserName.IsEmpty(), "用户名不能为空")
                .SplitError(x => !Tools.CheckUserName(x.Item.UserName), "用户名不符合规范")
                .WhereColumns(it => it.UserName)//如果不是主键可以这样实现（多字段it=>new{it.x1,it.x2}）
                .ToStorage();
            var result = x.AsInsertable.ExecuteCommand();//插入可插入部分;

            string msg = string.Format(" 插入{0} 更新{1} 错误数据{2} 不计算数据{3} 删除数据{4},总共{5}",
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

            return msg;
        }

        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="user">登录实体</param>
        /// <returns></returns>
        public SysUser Login(LoginBodyDto user)
        {
            return GetFirst(it => it.UserName == user.Username && it.Password == user.Password);
        }

        /// <summary>
        /// 修改登录信息
        /// </summary>
        /// <param name="user"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public void UpdateLoginInfo(LoginBodyDto user, long userId)
        {
            Update(new SysUser() { LoginIP = user.LoginIP, LoginDate = DateTime.Now, UserId = userId },it => new { it.LoginIP, it.LoginDate });
        }
    }
}
