using Infrastructure.Attribute;
using System;
using System.Collections.Generic;
using ZR.Model;
using ZR.Model.System;

namespace ZR.Repository.System
{
    /// <summary>
    /// 用户管理
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class SysUserRepository : BaseRepository
    {
        /// <summary>
        /// 根据条件分页查询用户列表
        /// <paramref name="user"/>
        /// <paramref name="user"/>
        /// </summary>
        /// <returns></returns>
        public List<SysUser> SelectUserList(SysUser user, PagerInfo pager)
        {
            string sql = @"SELECT u.*,d.deptName, d.leader
            FROM sys_user u 
            left join sys_dept d on u.deptId = d.deptId
            WHERE u.delFlag = '0' ";
            int totalCount = 0;
            var list = Db.SqlQueryable<SysUser>(sql)
                .WhereIF(!string.IsNullOrEmpty(user.UserName), it => it.UserName.Contains(user.UserName))
                .WhereIF(!string.IsNullOrEmpty(user.Status), it => it.Status == user.Status)
                .WhereIF(user.BeginTime != DateTime.MinValue && user.BeginTime != null, it => it.Create_time >= user.BeginTime)
                .WhereIF(user.EndTime != DateTime.MinValue && user.BeginTime != null, it => it.Create_time <= user.EndTime)
                .WhereIF(user.DeptId != 0, it => it.DeptId == user.DeptId)
                .OrderBy(it => it.UserId)
                .ToPageList(pager.PageNum, pager.PageSize, ref totalCount);
            pager.TotalNum = totalCount;
            return list;
        }

        /// <summary>
        /// 通过用户ID查询用户
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public SysUser SelectUserById(long userId)
        {
            return Db.Queryable<SysUser>().Where(f => f.UserId == userId).First();
        }

        /// <summary>
        /// 校验用户名是否存在
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public int CheckUserNameUnique(string userName)
        {
            return Db.Queryable<SysUser>().Where(it => it.UserName == userName).Count();
        }

        /// <summary>
        /// 添加用户
        /// </summary>
        /// <param name="sysUser"></param>
        /// <returns></returns>
        public int AddUser(SysUser sysUser)
        {
            sysUser.Create_time = DateTime.Now;
            return Db.Insertable(sysUser).ExecuteReturnIdentity();
        }

        /// <summary>
        /// 重置密码
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public int ResetPwd(long userid, string password)
        {
            return Db.Updateable(new SysUser() { UserId = userid, Password = password })
                .UpdateColumns(it => new { it.Password }).ExecuteCommand();
        }

        /// <summary>
        /// 改变用户状态
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public int ChangeUserStatus(SysUser user)
        {
            return Db.Updateable(user).UpdateColumns(t => new { t.Status })
                .ExecuteCommand();
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="userid">用户id</param>
        /// <returns></returns>
        public int DeleteUser(long userid)
        {
            return Db.Updateable(new SysUser() { UserId = userid, DelFlag = "2" })
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
            return Db.Updateable(user)
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
            return Db.Updateable<SysUser>()
                .SetColumns(t => new SysUser()
                {
                    Avatar = user.Avatar
                })
                .Where(f => f.UserId == user.UserId).ExecuteCommand();
        }
    }
}
