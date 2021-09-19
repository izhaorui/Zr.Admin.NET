using Infrastructure;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZR.CodeGenerator
{
    /// <summary>
    /// 代码生成数据库连接
    /// </summary>
    public class DbProvider
    {
        protected static SqlSugarScope CodeDb;

        /// <summary>
        /// 获取动态连接字符串
        /// </summary>
        /// <param name="dbName">数据库名</param>
        /// <returns></returns>
        public SqlSugarScope GetSugarDbContext(string dbName = "")
        {
            string connStr = ConfigUtils.Instance.GetConfig(GenConstants.Gen_conn);
            int dbType = ConfigUtils.Instance.GetAppConfig(GenConstants.Gen_conn_dbType, 0);
            connStr = connStr.Replace("{database}", dbName);
            if (string.IsNullOrEmpty(dbName))
            {
                connStr = ConfigUtils.Instance.GetConnectionStrings(OptionsSetting.ConnAdmin);
                dbType = ConfigUtils.Instance.GetAppConfig<int>(OptionsSetting.DbType);
            }
            var db = new SqlSugarScope(new List<ConnectionConfig>()
            {
                new ConnectionConfig(){
                    ConnectionString = connStr,
                    DbType = (DbType)dbType,
                    IsAutoCloseConnection = true,//开启自动释放模式和EF原理一样
                    InitKeyType = InitKeyType.Attribute,//从特性读取主键和自增列信息
                },
            });

            CodeDb = db;
            return db;
        }
    }
}
