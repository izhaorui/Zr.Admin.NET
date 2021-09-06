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
    [AppService(ServiceType = typeof(ICodeGeneratorService), ServiceLifetime = LifeTime.Transient)]
    public class CodeGeneratorService: ICodeGeneratorService
    {
        public CodeGeneratorRepository CodeGeneratorRepository;

        public CodeGeneratorService(CodeGeneratorRepository codeGeneratorRepository)
        {
            CodeGeneratorRepository = codeGeneratorRepository;
        }

        /// <summary>
        /// 获取所有数据库名
        /// </summary>
        /// <returns></returns>
        public List<DataBaseInfo> GetAllDataBases(string dbType)
        {
            List<DataBaseInfo> list = new List<DataBaseInfo>();
            if (dbType.Contains("SqlServer"))
            {
                list = CodeGeneratorRepository.GetAllDataBaseInfos();
            }
            else if (dbType.Contains("MySql"))
            {
               // list = mssqlExtractor.GetAllDataBases();
            }
            return list;
        }

        public List<DbTableInfo> GetTablesWithPage(string tablename, string dbName, PagerInfo info)
        {
            var dbType = ConfigUtils.Instance.GetConfig("CodeGenDbType");
            List<DbTableInfo> list = new List<DbTableInfo>();
            if (dbType == "1")
            {
                list = CodeGeneratorRepository.GetAllTables(dbName);
            }
            else if (dbType.Contains("MySql"))
            {
                //list = mysqlExtractor.GetAllTables(this.dbName, tablename, fieldNameToSort, isDescending, info);
            }
            if (!string.IsNullOrEmpty(tablename))
            {
                list = list.Where(f => f.TableName.Contains(tablename)).ToList();
            }

            return list;
        }

    }
}
