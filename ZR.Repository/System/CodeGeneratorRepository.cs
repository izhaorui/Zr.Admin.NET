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
    public class CodeGeneratorRepository : BaseRepository
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
        /// <param name="dbName"></param>
        /// <param name="pager"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public List<DbTableInfo> GetAllTables(string dbName, string tableName, PagerInfo pager)
        {
            string sql = $"SELECT name as TableName FROM {dbName}..SysObjects Where XType='U'";
            int total = 0;
            var list = Db.SqlQueryable<DbTableInfo>(sql)
                //.WithCache(60 * 10)
                .WhereIF(!string.IsNullOrEmpty(tableName), it => it.TableName.Contains(tableName))
                .AddParameters(new { dbName })
                .OrderBy(x => x.TableName)
                .ToPageList(pager.PageNum, pager.PageSize, ref total);
            pager.TotalNum = total;
            return list;
        }
    }
}
