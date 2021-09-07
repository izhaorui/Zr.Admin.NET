using Infrastructure;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZR.Model;
using ZR.Model.CodeGenerator;

namespace ZR.CodeGenerator.Service
{
    public class CodeGeneraterService: DbProvider
    {
        ///// <summary>
        ///// 获取表所有列
        ///// </summary>
        ///// <param name="tableName"></param>
        ///// <returns></returns>
        //public List<DbFieldInfo> GetAllColumns(string tableName)
        //{
        //    var dbType = ConfigUtils.Instance.GetConfig("CodeGenDbType");
        //    if (tableName == null)
        //        throw new ArgumentException(nameof(tableName));
        //    List<DbFieldInfo> list = new List<DbFieldInfo>();
        //    if (dbType == "1")
        //    {
        //        list = CodeGeneratorRepository.GetAllColumns(tableName);
        //    }
        //    return list;
        //}

        /// <summary>
        /// 获取所有数据库名
        /// </summary>
        /// <returns></returns>
        public List<DataBaseInfo> GetAllDataBases()
        {
            var dbType = ConfigUtils.Instance.GetConfig("CodeGenDbType");
            List<DataBaseInfo> list = new List<DataBaseInfo>();
            if (dbType == "1")
            {
                var db = GetSugarDbContext("ZrAdmin");
                var templist = db.DbMaintenance.GetDataBaseList(db);
                templist.ForEach(item =>
                {
                    list.Add(new DataBaseInfo() { DbName = item });
                });
            }
            else if (dbType == "0")
            {
                // list = mssqlExtractor.GetAllDataBases();
            }
            return list;
        }

        /// <summary>
        /// 获取所有表
        /// </summary>
        /// <param name="dbName"></param>
        /// <param name="tableName"></param>
        /// <param name="tableStrs"></param>
        /// <param name="pager"></param>
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
        /// 获取列信息
        /// </summary>
        /// <param name="dbName"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public List<DbColumnInfo> GetColumnInfo(string dbName, string tableName)
        {
           return GetSugarDbContext(dbName).DbMaintenance.GetColumnInfosByTableName(tableName, true);
        }

    }
}
