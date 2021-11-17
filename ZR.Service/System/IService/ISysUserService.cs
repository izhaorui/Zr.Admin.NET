﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZR.Model;
using ZR.Model.System;

namespace ZR.Service.System.IService
{
    public interface ISysUserService
    {
        public List<SysUser> SelectUserList(SysUser user, PagerInfo pager);

        /// <summary>
        /// 获取归属指定部门的用户数量
        /// </summary>
        /// <returns></returns>
        public int GetDeptUserCount(long deptId);

        /// <summary>
        /// 通过用户ID查询用户
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public SysUser SelectUserById(long userId);

        /// <summary>
        /// 校验用户名称是否唯一
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public string CheckUserNameUnique(string userName);

        /// <summary>
        /// 新增保存用户信息
        /// </summary>
        /// <param name="sysUser"></param>
        /// <returns></returns>
        public long InsertUser(SysUser sysUser);

        /// <summary>
        /// 修改用户信息
        /// </summary>
        /// <param name="sysUser"></param>
        /// <returns></returns>
        public int UpdateUser(SysUser user);

        public int ChangeUser(SysUser user);

        /// <summary>
        /// 重置密码
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public int ResetPwd(long userid, string password);

        public int ChangeUserStatus(SysUser user);

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public int DeleteUser(long userid);

        /// <summary>
        /// 修改用户头像
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public int UpdatePhoto(SysUser user);
    }
}
