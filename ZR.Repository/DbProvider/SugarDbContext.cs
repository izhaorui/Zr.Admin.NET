using Infrastructure;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ZR.Repository.DbProvider
{
    /// <summary>
    /// SqlSugar ORM
    /// </summary>
    public class SugarDbContext
    {
        public SqlSugarClient Db;   //用来处理事务多表查询和复杂的操作

        /// <summary>
        /// 使用SugarSql获取连接对象
        /// 构造方法有问题
        /// </summary>
        /// <returns></returns>
        public SugarDbContext()
        {
            string connStr = ConfigUtils.Instance.GetConnectionStrings(OptionsSetting.ConnAdmin);
            string dbKey = ConfigUtils.Instance.GetAppConfig<string>(OptionsSetting.DbKey);
            int dbType = ConfigUtils.Instance.GetAppConfig(OptionsSetting.DbType, 0);
            if (!string.IsNullOrEmpty(dbKey))
            {
                connStr = NETCore.Encrypt.EncryptProvider.DESDecrypt(connStr, dbKey);
            }
            Db = new SqlSugarClient(new List<ConnectionConfig>()
            {
                new ConnectionConfig(){
                    ConnectionString = connStr,
                    DbType = (DbType)dbType,
                    IsAutoCloseConnection = true,//开启自动释放模式和EF原理一样
                    InitKeyType = InitKeyType.Attribute,//从特性读取主键和自增列信息
                    ConfigId = 0
                },
            });

            //调式代码 用来打印SQL 
            Db.Aop.OnLogExecuting = (sql, pars) =>
            {
                Console.WriteLine("【SQL语句】" + sql.ToLower() + "\r\n" + Db.Utilities.SerializeObject(pars.ToDictionary(it => it.ParameterName, it => it.Value)));
            };
            //出错打印日志
            Db.Aop.OnError = (e) =>
            {
                Console.WriteLine($"[执行Sql出错]{e.Message}，SQL={e.Sql}");
                Console.WriteLine();
            };
        }
    }
}
