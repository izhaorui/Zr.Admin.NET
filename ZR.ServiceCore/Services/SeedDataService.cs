using Infrastructure;
using MiniExcelLibs;
using SqlSugar.IOC;
using ZR.Common;
using ZR.Model.Content;
using ZR.Model.System;
using ZR.Model.System.Tenant;
using ZR.ServiceCore.Services.Seed;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 种子数据处理
    /// </summary>
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
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())//表示如果有where条件根据条件判断是否存在数据，不存在插入，存在不操作
                .SplitError(x => x.Item.UserName.IsEmpty(), "用户名不能为空")
                .SplitError(x => !Tools.CheckUserName(x.Item.UserName), "用户名不符合规范")
                .WhereColumns(it => it.UserId)//如果不是主键可以这样实现（多字段it=>new{it.x1,it.x2}）
                .ToStorage();
            var result = x.AsInsertable.OffIdentity().ExecuteCommand();//插入可插入部分;

            string msg = $"[用户数据] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
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
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.MenuId)//如果不是主键可以这样实现（多字段it=>new{it.x1,it.x2}）
                .ToStorage();
            var result = x.AsInsertable.OffIdentity().ExecuteCommand();//插入可插入部分;

            string msg = $"[菜单数据] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
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

            string msg = $"[角色菜单] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
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
            var result = x.AsInsertable.OffIdentity().ExecuteCommand();

            string msg = $"[部门数据] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
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

            string msg = $"[岗位数据] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
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

            string msg = $"[角色数据] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
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

            string msg = $"[用户角色] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
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

            string msg = $"[系统配置] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
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

            string msg = $"[字典管理] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
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
                .WhereColumns(it => new { it.DictType, it.DictValue })
                .ToStorage();
            x.AsInsertable.ExecuteCommand();
            x.AsUpdateable.ExecuteCommand();

            string msg = $"[字典数据] 插入{x.InsertList.Count} 更新{x.UpdateList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
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
                //.SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.Name)
                .ToStorage();
            x.AsInsertable.ExecuteCommand();
            x.AsUpdateable.ExecuteCommand();
            string msg = $"[文章目录] 插入{x.InsertList.Count} 更新{x.UpdateList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 文章话题
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitArticleTopicData(List<ArticleTopic> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .WhereColumns(it => it.TopicName)
                .ToStorage();
            x.AsInsertable.ExecuteCommand();
            x.AsUpdateable.ExecuteCommand();
            string msg = $"[文章话题] 插入{x.InsertList.Count} 更新{x.UpdateList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
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

            string msg = $"[任务数据] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 公告数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitNoticeData(List<SysNotice> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .WhereColumns(it => new { it.NoticeId })
                .ToStorage();
            x.AsInsertable.ExecuteCommand();
            x.AsUpdateable.ExecuteCommand();

            string msg = $"[通知公告数据] 插入{x.InsertList.Count} 更新{x.UpdateList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 确保主库默认租户存在（幂等，直接查库判断）。作为种子数据的兜底：
        /// 即使 data.xlsx 无 tenant 页或未执行全量种子，也能保证主库租户元信息存在。
        /// 原分散在 InitTable.EnsureDefaultTenant，已统一收敛至此。
        /// </summary>
        public string EnsureDefaultTenant()
        {
            var mainDb = App.MainDbConfigId;
            var db = DbScoped.SugarScope;

            if (db.Queryable<SysTenant>().Any(x => x.DelFlag == 0 && x.TenantId == mainDb))
                return "[默认租户] 已存在，跳过";

            db.Insertable(new SysTenant
            {
                TenantId = mainDb,
                TenantName = "默认租户",
                CompanyName = "默认租户",
                Status = 0,
                DelFlag = 0,
                Remark = "种子初始化自动补齐"
            }).ExecuteCommand();

            return "[默认租户] 已创建";
        }

        /// <summary>
        /// 初始化种子数据
        /// </summary>
        /// <param name="path"></param>
        /// <param name="clean"></param>
        /// <returns></returns>
        public List<string> InitSeedData(string path, bool clean)
        {
            List<string> result = new();

            var db = DbScoped.SugarScope;

            // 先读取 Excel 数据（不依赖事务）
            var sysUser = MiniExcel.Query<SysUser>(path, sheetName: "user").ToList();
            var sysPost = MiniExcel.Query<SysPost>(path, sheetName: "post").ToList();
            var sysRole = MiniExcel.Query<SysRole>(path, sheetName: "role").ToList();
            var sysUserRole = MiniExcel.Query<SysUserRole>(path, sheetName: "user_role").ToList();
            var sysMenu = MiniExcel.Query<SysMenu>(path, sheetName: "menu").ToList();
            var sysConfig = MiniExcel.Query<SysConfig>(path, sheetName: "config").ToList();
            var sysRoleMenu = MiniExcel.Query<SysRoleMenu>(path, sheetName: "role_menu").ToList();
            var sysDict = MiniExcel.Query<SysDictType>(path, sheetName: "dict_type").ToList();
            var sysDictData = MiniExcel.Query<SysDictData>(path, sheetName: "dict_data").ToList();
            var sysDept = MiniExcel.Query<SysDept>(path, sheetName: "dept").ToList();
            var sysArticleCategory = MiniExcel.Query<ArticleCategory>(path, sheetName: "article_category").ToList();
            var sysNotice = MiniExcel.Query<SysNotice>(path, sheetName: "notice").ToList();

            //var sysTenant = MiniExcel.Query<SysTenant>(path, sheetName: "tenant").ToList();

            try
            {
                db.Ado.BeginTran();

                if (clean)
                {
                    db.DbMaintenance.TruncateTable<SysRoleDept>();
                    db.DbMaintenance.TruncateTable<SysRoleMenu>();
                    db.DbMaintenance.TruncateTable<SysMenu>();
                    db.DbMaintenance.TruncateTable<SysRole>();
                    db.DbMaintenance.TruncateTable<SysUser>();
                    db.DbMaintenance.TruncateTable<SysDept>();
                    db.DbMaintenance.TruncateTable<SysPost>();
                    db.DbMaintenance.TruncateTable<SysDictType>();
                    db.DbMaintenance.TruncateTable<SysDictData>();
                    db.DbMaintenance.TruncateTable<SysNotice>();
                    db.DbMaintenance.TruncateTable<SysUserRole>();
                    db.DbMaintenance.TruncateTable<SysTenant>();
                    db.DbMaintenance.TruncateTable<SysTenantPlan>();
                    db.DbMaintenance.TruncateTable<SysTenantPlanBinding>();
                    db.DbMaintenance.TruncateTable<SysTenantPlanMenu>();
                }

                result.Add(InitUserData(sysUser).Item1);
                result.Add(InitPostData(sysPost).Item1);
                result.Add(InitRoleData(sysRole).Item1);
                result.Add(InitUserRoleData(sysUserRole).Item1);
                result.Add(InitMenuData(sysMenu).Item1);
                result.Add(InitConfigData(sysConfig).Item1);
                result.Add(InitRoleMenuData(sysRoleMenu).Item1);
                result.Add(InitDictType(sysDict).Item1);
                result.Add(InitDictData(sysDictData).Item1);
                result.Add(InitDeptData(sysDept).Item1);
                result.Add(InitArticleCategoryData(sysArticleCategory).Item1);
                result.Add(InitNoticeData(sysNotice).Item1);
                result.Add(EnsureDefaultTenant());

                result.Add(new SystemTaskSeedService().EnsureSystemTasksSeedData());
                result.Add(new MallSeedService().EnsureTasksSeedData());

                db.Ado.CommitTran();
            }
            catch (Exception ex)
            {
                db.Ado.RollbackTran();
                result.Add($"[种子数据初始化失败] 事务已回滚: {ex.Message}");
            }

            // 独立模块菜单种子：按 appsettings 的 InitMall/InitWorkflow/InitSaasMenu 开关决定是否写入，
            // 与 CLI --initdb 链路（ModuleInitRunner.RunEnabledModules）行为保持一致。
            // 放在主事务之外，避免模块菜单写入失败连带回滚核心种子；菜单种子本身幂等。
            try
            {
                var options = App.OptionsSetting;
                if (options != null)
                {
                    if (options.InitMall) result.AddRange(InitMallMenuSeedData());
                    if (options.InitWorkflow) result.AddRange(InitWorkflowMenuSeedData());
                    if (options.InitSaasMenu) result.AddRange(InitSaasMenuSeedData());
                }
            }
            catch (Exception ex)
            {
                result.Add($"[独立模块菜单种子失败] {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 单独初始化商城模块：创建商城菜单与按钮权限，并纳入默认套餐使其对租户可见。
        /// 委托给 MallSeedService，由 CLI --initdb 触发、按 InitMall 开关决定是否执行。
        /// </summary>
        public List<string> InitMallMenuSeedData()
        {
            return new MallSeedService().InitMenuSeedData();
        }

        /// <summary>
        /// 单独初始化工作流模块：创建工作流菜单与按钮权限，并纳入默认套餐使其对租户可见。
        /// 委托给 WorkflowSeedService，由 CLI --initdb 触发、按 InitWorkflow 开关决定是否执行。
        /// </summary>
        public List<string> InitWorkflowMenuSeedData()
        {
            return new WorkflowSeedService().InitMenuSeedData();
        }

        /// <summary>
        /// 初始化saas模块菜单种子数据（租户管理、套餐菜单、字典种子）。
        /// </summary>
        public List<string> InitSaasMenuSeedData()
        {
            return new SaasMenuSeedService().EnsureAllSeedMenus();
        }
    }
}
