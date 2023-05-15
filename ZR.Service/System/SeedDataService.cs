using Infrastructure.Extensions;
using SqlSugar.IOC;
using System.Collections.Generic;
using ZR.Common;
using ZR.Model.System;

namespace ZR.Service.System
{
    public class SeedDataService
    {
        /// <summary>
        /// 初始化用户数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitUserData(List<SysUser> data)
        {
            data.ForEach(x =>
            {
                x.Password = "E10ADC3949BA59ABBE56E057F20F883E";
            });
            var db = DbScoped.SugarScope;
            db.Ado.BeginTran();
            //db.Ado.ExecuteCommand("SET IDENTITY_INSERT sys_user ON");
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .SplitError(x => x.Item.UserName.IsEmpty(), "用户名不能为空")
                .SplitError(x => !Tools.CheckUserName(x.Item.UserName), "用户名不符合规范")
                .WhereColumns(it => it.UserId)//如果不是主键可以这样实现（多字段it=>new{it.x1,it.x2}）
                .ToStorage();
            var result = x.AsInsertable.OffIdentity().ExecuteCommand();//插入可插入部分;
            //db.Ado.ExecuteCommand("SET IDENTITY_INSERT sys_user OFF");
            db.Ado.CommitTran();

            string msg = $"[用户数据] 插入{x.InsertList.Count} 错误数据{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 菜单数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitMenuData(List<SysMenu> data)
        {
            var db = DbScoped.SugarScope;
            db.Ado.BeginTran();
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.MenuId)//如果不是主键可以这样实现（多字段it=>new{it.x1,it.x2}）
                .ToStorage();
            var result = x.AsInsertable.OffIdentity().ExecuteCommand();//插入可插入部分;
            db.Ado.CommitTran();

            string msg = $"[菜单数据] 插入{x.InsertList.Count} 错误数据{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }
        /// <summary>
        /// 角色菜单数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitRoleMenuData(List<SysRoleMenu> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => new { it.Menu_id, it.Role_id })
                .ToStorage();
            var result = x.AsInsertable.ExecuteCommand();//插入可插入部分;

            string msg = $"[角色菜单] 插入{x.InsertList.Count} 错误数据{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }
        /// <summary>
        /// 初始化部门数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitDeptData(List<SysDept> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.DeptId)
                .ToStorage();
            var result = x.AsInsertable.ExecuteCommand();

            string msg = $"[部门数据] 插入{x.InsertList.Count} 错误数据{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        public (string, object, object) InitPostData(List<SysPost> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.PostCode)
                .ToStorage();
            var result = x.AsInsertable.ExecuteCommand();

            string msg = $"[岗位数据] 插入{x.InsertList.Count} 错误数据{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        public (string, object, object) InitRoleData(List<SysRole> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.RoleKey)
                .ToStorage();
            var result = x.AsInsertable.OffIdentity().ExecuteCommand();

            string msg = $"[角色数据] 插入{x.InsertList.Count} 错误数据{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        public (string, object, object) InitUserRoleData(List<SysUserRole> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => new { it.RoleId, it.UserId })
                .ToStorage();
            var result = x.AsInsertable.ExecuteCommand();

            string msg = $"[用户角色] 插入{x.InsertList.Count} 错误数据{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 系统配置
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitConfigData(List<SysConfig> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.ConfigKey)
                .ToStorage();
            var result = x.AsInsertable.ExecuteCommand();

            string msg = $"[系统配置] 插入{x.InsertList.Count} 错误数据{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 字典
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitDictType(List<SysDictType> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.DictType)
                .ToStorage();
            var result = x.AsInsertable.ExecuteCommand();

            string msg = $"[字典管理] 插入{x.InsertList.Count} 错误数据{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 字典数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitDictData(List<SysDictData> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => new { it.DictType, it.DictValue })
                .ToStorage();
            var result = x.AsInsertable.ExecuteCommand();

            string msg = $"[字典数据] 插入{x.InsertList.Count} 错误数据{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 文章目录
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitArticleCategoryData(List<ArticleCategory> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.Name)
                .ToStorage();
            var result = x.AsInsertable.OffIdentity().ExecuteCommand();

            string msg = $"[字典数据] 插入{x.InsertList.Count} 错误数据{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 任务
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitTaskData(List<SysTasks> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.Name)
                .ToStorage();
            var result = x.AsInsertable.ExecuteCommand();

            string msg = $"[任务数据] 插入{x.InsertList.Count} 错误数据{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }
    }
}
