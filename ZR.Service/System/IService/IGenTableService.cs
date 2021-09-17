using Infrastructure.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZR.Model.System.Generate;

namespace ZR.Service.System.IService
{
    public interface IGenTableService
    {
        List<GenTable> SelectDbTableListByNamess(string[] tableNames);

        int InsertGenTable(GenTable tables);

        int DeleteGenTable(GenTable table);
        PagedInfo<GenTable> GetGenTables(GenTable genTable, Model.PagerInfo pagerInfo);
        GenTable GetGenTableInfo(long tableId);
    }

    public interface IGenTableColumnService
    {
        int InsertGenTableColumn(List<GenTableColumn> tableColumn);

        int DeleteGenTableColumn(long tableId);

        List<GenTableColumn> GenTableColumns(long tableId);
    }
}
