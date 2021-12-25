using Infrastructure.Attribute;
using Infrastructure.Extensions;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using ZR.Model;
using ZR.Model.System;

namespace ZR.Repository.System
{
    /// <summary>
    /// 用户管理
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class SysUserRepository : BaseRepository<SysUser>
    {
        /// <summary>
        /// 根据条件分页查询用户列表
        /// <paramref name="user"/>
        /// <paramref name="user"/>
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
                SysDept dept = Context.Queryable<SysDept>().First(f => f.DeptId == user.DeptId);
                string[] deptArr = dept?.Ancestors.Split(",").ToArray();

                exp.AndIF(user.DeptId != 0, u => u.DeptId == user.DeptId);// || deptArr.Contains(u.DeptId.ToString()));
            }
            var query = Context.Queryable<SysUser>()
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
            return Context.Queryable<SysUser>().Where(f => f.UserId == userId).First();
        }

        /// <summary>
        /// 校验用户名是否存在
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public int CheckUserNameUnique(string userName)
        {
            return Context.Queryable<SysUser>().Where(it => it.UserName == userName).Count();
        }

        /// <summary>
        /// 添加用户
        /// </summary>
        /// <param name="sysUser"></param>
        /// <returns></returns>
        public int AddUser(SysUser sysUser)
        {
            sysUser.Create_time = DateTime.Now;
            return Context.Insertable(sysUser).ExecuteReturnIdentity();
        }

        /// <summary>
        /// 重置密码
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public int ResetPwd(long userid, string password)
        {
            return Context.Updateable(new SysUser() { UserId = userid, Password = password })
                .UpdateColumns(it => new { it.Password }).ExecuteCommand();
        }

        /// <summary>
        /// 改变用户状态
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public int ChangeUserStatus(SysUser user)
        {
            return Context.Updateable(user).UpdateColumns(t => new { t.Status })
                .ExecuteCommand();
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="userid">用户id</param>
        /// <returns></returns>
        public int DeleteUser(long userid)
        {
            return Context.Updateable(new SysUser() { UserId = userid, DelFlag = "2" })
                .UpdateColumns(t => t.DelFlag)
                .ExecuteCommand();
        }

        /// <summary>
        /// 修改用户信息
        /// </summary>
        /// <param name="sysUser"></param>
        /// <returns></returns>
        public int UpdateUser(SysUser user)
        {
            return Context.Updateable(user)
                //.SetColumns(t => new SysUser()
                //{
                //    UserName = user.UserName,
                //    Status = user.Status,
                //    NickName = user.NickName,
                //    Remark = user.Remark,
                //    Email = user.Email,
                //    Update_by = user.Update_by,
                //    Phonenumber = user.Phonenumber,
                //    Update_time = DateTime.Now,
                //    Sex = user.Sex,
                //    DeptId = user.DeptId
                //})
                .IgnoreColumns(ignoreAllNullColumns: true)//忽略所有为null
                .IgnoreColumns(it => new { it.Password, it.Avatar })
                .Where(f => f.UserId == user.UserId).ExecuteCommand();
        }

        /// <summary>
        /// 修改用户头像
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public int UpdatePhoto(SysUser user)
        {
            return Context.Updateable<SysUser>()
                .SetColumns(t => new SysUser()
                {
                    Avatar = user.Avatar
                })
                .Where(f => f.UserId == user.UserId).ExecuteCommand();
        }
    }
}
