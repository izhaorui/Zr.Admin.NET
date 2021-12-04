using System.Collections.Generic;
using ZR.Model;
using ZR.Model.System.Generate;

namespace ZR.Service.System.IService
{
    public interface IGenTableService: IBaseService<GenTable>
    {
        List<GenTable> SelectDbTableListByNamess(string[] tableNames);

        int ImportGenTable(GenTable tables);

        int DeleteGenTableByIds(long[] tableIds);
        int DeleteGenTableByTbName(string tableName);
        PagedInfo<GenTable> GetGenTables(GenTable genTable, Model.PagerInfo pagerInfo);
        GenTable GetGenTableInfo(long tableId);
        int UpdateGenTable(GenTable genTable);
    }

    public interface IGenTableColumnService: IBaseService<GenTableColumn>
    {        
        int InsertGenTableColumn(List<GenTableColumn> tableColumn);

        int DeleteGenTableColumn(long tableId);
        int DeleteGenTableColumn(long[] tableIds);
        int DeleteGenTableColumnByTableName(string tableName);
        List<GenTableColumn> GenTableColumns(long tableId);
        int UpdateGenTableColumn(List<GenTableColumn> tableColumn);
    }
}
