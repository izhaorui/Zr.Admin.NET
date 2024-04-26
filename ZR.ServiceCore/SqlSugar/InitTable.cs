using SqlSugar.IOC;
using ZR.Model.Models;
using ZR.Model.System;
using ZR.Model.System.Generate;
using ZR.ServiceCore.Model;

namespace ZR.ServiceCore.SqlSugar
{
    /// <summary>
    /// 初始化表
    /// </summary>
    public class InitTable
    {
        /// <summary>
        /// 创建db、表
        /// </summary>
        public static void InitDb(bool init)
        {
            var db = DbScoped.SugarScope;
            //TODO 可在此处单独更新某个表的结构
            //例如：db.CodeFirst.InitTables(typeof(EmailLog));
            

            if (!init) return;
            //建库：如果不存在创建数据库存在不会重复创建 
            db.DbMaintenance.CreateDatabase();// 注意 ：Oracle和个别国产库需不支持该方法，需要手动建库 

            //27个表,建议先使用下面方法初始化表，方便排查问题
            db.CodeFirst.InitTables(typeof(SysUser));
            db.CodeFirst.InitTables(typeof(SysRole));
            db.CodeFirst.InitTables(typeof(SysDept));
            db.CodeFirst.InitTables(typeof(SysPost));
            db.CodeFirst.InitTables(typeof(SysFile));
            db.CodeFirst.InitTables(typeof(SysConfig));
            db.CodeFirst.InitTables(typeof(SysNotice));
            db.CodeFirst.InitTables(typeof(SysLogininfor));
            db.CodeFirst.InitTables(typeof(SysOperLog));
            db.CodeFirst.InitTables(typeof(SysMenu));
            db.CodeFirst.InitTables(typeof(SysRoleMenu));
            db.CodeFirst.InitTables(typeof(SysRoleDept));
            db.CodeFirst.InitTables(typeof(SysUserRole));
            db.CodeFirst.InitTables(typeof(SysUserPost));
            db.CodeFirst.InitTables(typeof(SysTasks));
            db.CodeFirst.InitTables(typeof(SysTasksLog));
            db.CodeFirst.InitTables(typeof(CommonLang));
            db.CodeFirst.InitTables(typeof(GenTable));
            db.CodeFirst.InitTables(typeof(GenTableColumn));
            db.CodeFirst.InitTables(typeof(Article));
            db.CodeFirst.InitTables(typeof(ArticleCategory));
            db.CodeFirst.InitTables(typeof(ArticleBrowsingLog));
            db.CodeFirst.InitTables(typeof(SysDictData));
            db.CodeFirst.InitTables(typeof(SysDictType));
            db.CodeFirst.InitTables(typeof(SqlDiffLog));
            db.CodeFirst.InitTables(typeof(EmailTpl));
            db.CodeFirst.InitTables(typeof(SmsCodeLog));
            db.CodeFirst.InitTables(typeof(EmailLog));
            db.CodeFirst.InitTables(typeof(ArticlePraise));
            db.CodeFirst.InitTables(typeof(ArticleComment));
            //db.CodeFirst.InitTables(typeof(UserOnlineLog));
        }
        public static void InitNewTb()
        {
            var db = DbScoped.SugarScope;
            var t1 = db.DbMaintenance.IsAnyTable(typeof(UserOnlineLog).Name);
            if (!t1)
            {
                db.CodeFirst.InitTables(typeof(UserOnlineLog));
            }
        }
    }
}
