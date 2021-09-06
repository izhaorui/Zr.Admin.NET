using Infrastructure.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZR.Model;
using ZR.Model.CodeGenerator;

namespace ZR.Repository.System
{
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class CodeGeneratorRepository: BaseRepository
    {
        /// <summary>
        /// 获取数据库信息
        /// </summary>
        /// <returns></returns>
        public List<DataBaseInfo> GetAllDataBaseInfos()
        {
            return Db.Ado.SqlQuery<DataBaseInfo>("select name as DbName from master..sysdatabases ");
        }

        /// <summary>
        /// 获取所有的表
        /// </summary>
        /// <returns></returns>
        public List<DbTableInfo> GetAllTables(string dbName)
        {
            string sql = $"SELECT name as TableName FROM {dbName}..SysObjects Where XType='U' ORDER BY Name";
            return Db.Ado.SqlQuery<DbTableInfo>(sql, new { dbName});
        }
    }
}
