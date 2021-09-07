using Infrastructure;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZR.CodeGenerator
{
    public class DbProvider
    {

        public SqlSugarClient GetSugarDbContext(string dbName)
        {
            string connStr = ConfigUtils.Instance.GetConnectionStrings(OptionsSetting.Conn).Replace("{DbName}", dbName);
            int dbType = ConfigUtils.Instance.GetAppConfig(OptionsSetting.DbType, 0);

            return new SqlSugarClient(new List<ConnectionConfig>()
            {
                new ConnectionConfig(){
                    ConnectionString = connStr,
                    DbType = (DbType)dbType,
                    IsAutoCloseConnection = true,//开启自动释放模式和EF原理一样
                    InitKeyType = InitKeyType.Attribute,//从特性读取主键和自增列信息
                },
            });
        }
    }
}
