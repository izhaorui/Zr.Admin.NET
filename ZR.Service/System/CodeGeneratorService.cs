using Infrastructure;
using Infrastructure.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZR.Model;
using ZR.Model.CodeGenerator;
using ZR.Repository.System;
using ZR.Service.IService;

namespace ZR.Service.System
{
    /// <summary>
    /// 
    /// </summary>
    //[AppService(ServiceType = typeof(ICodeGeneratorService), ServiceLifetime = LifeTime.Transient)]
    public class CodeGeneratorService //: ICodeGeneratorService
    {
        //public CodeGeneratorRepository CodeGeneratorRepository;

        //public CodeGeneratorService(CodeGeneratorRepository codeGeneratorRepository)
        //{
        //    CodeGeneratorRepository = codeGeneratorRepository;
        //}

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

        ///// <summary>
        ///// 获取所有数据库名
        ///// </summary>
        ///// <returns></returns>
        //public List<DataBaseInfo> GetAllDataBases()
        //{
        //    var dbType = ConfigUtils.Instance.GetConfig("CodeGenDbType");
        //    List<DataBaseInfo> list = new List<DataBaseInfo>();
        //    if (dbType == "1")
        //    {
        //        list = CodeGeneratorRepository.GetAllDb();
        //    }
        //    else if (dbType == "0")
        //    {
        //        // list = mssqlExtractor.GetAllDataBases();
        //    }
        //    return list;
        //}

        //public List<DbTableInfo> GetAllTables(string tableStrs)
        //{
        //    string[] tabList = tableStrs.Split(",");

        //    return CodeGeneratorRepository.GetAllTables(tabList);
        //}

        /// <summary>
        /// 获取列信息
        /// </summary>
        /// <param name="dbName"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        //public List<SqlSugar.DbColumnInfo> GetColumnInfo(string dbName, string tableName)
        //{
        //    return CodeGeneratorRepository.GetColumnInfo(dbName, tableName);
        //}

        //public List<SqlSugar.DbTableInfo> GetTablesWithPage(string tablename, string dbName, PagerInfo pager)
        //{
        //    var dbType = ConfigUtils.Instance.GetConfig("CodeGenDbType");
        //    List<SqlSugar.DbTableInfo> list = new List<SqlSugar.DbTableInfo>();
        //    if (dbType == "1")
        //    {
        //        list = CodeGeneratorRepository.GetAllTables(dbName, tablename, pager);
        //    }
        //    else if (dbType == "0")
        //    {
        //        //list = mysqlExtractor.GetAllTables(this.dbName, tablename, fieldNameToSort, isDescending, info);
        //    }
        //    return list;
        //}

    }
}
