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
        /// 获取数据库
        /// </summary>
        /// <returns></returns>
        public List<DataBaseInfo> GetAllDb()
        {
            //return Db.Ado.SqlQuery<DataBaseInfo>("select name as DbName from master..sysdatabases ");
            var list = Db.DbMaintenance.GetDataBaseList(Db);
            List<DataBaseInfo> dataBases = new List<DataBaseInfo>();
            list.ForEach(item =>
            {
                dataBases.Add(new DataBaseInfo() { DbName = item });
            });
            return dataBases;
        }

        /// <summary>
        /// 根据数据库名获取所有的表
        /// </summary>
        /// <param name="dbName"></param>
        /// <param name="pager"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public List<SqlSugar.DbTableInfo> GetAllTables(string dbName, string tableName, PagerInfo pager)
        {
            var tableList = GetSugarDbContext(dbName).DbMaintenance.GetTableInfoList(true);
            if (!string.IsNullOrEmpty(tableName))
            {
                tableList = tableList.Where(f => f.Name.Contains(tableName)).ToList();
            }
            pager.TotalNum = tableList.Count;
            return tableList.Skip(pager.PageSize * (pager.PageNum - 1)).Take(pager.PageSize).OrderBy(f => f.Name).ToList();
        }

        /// <summary>
        /// 获取表格列信息
        /// </summary>
        /// <param name="dbName"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public List<SqlSugar.DbColumnInfo> GetColumnInfo(string dbName, string tableName)
        {
            return GetSugarDbContext(dbName).DbMaintenance.GetColumnInfosByTableName(tableName, true);
        }


        /// <summary>
        /// 获取当前数据库表名
        /// </summary>
        /// <param name="tabList"></param>
        /// <returns></returns>
        public List<DbTableInfo> GetAllTables(string[] tabList)
        {
            string sql = @"SELECT tbs.name as TableName ,ds.value as Description FROM sys.tables tbs
                        left join sys.extended_properties ds on ds.major_id=tbs.object_id and ds.minor_id=0";

            return Db.SqlQueryable<DbTableInfo>(sql).WhereIF(tabList.Length > 0, x => tabList.Contains(x.TableName)).ToList();
        }
    }
}
