using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZR.Model;
using ZR.Model.CodeGenerator;

namespace ZR.Service.IService
{
    public interface ICodeGeneratorService
    {
        List<DataBaseInfo> GetAllDataBases(string dbType);

        List<DbTableInfo> GetTablesWithPage(string tablename, string dbName, PagerInfo info);
    }
}
