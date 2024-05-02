using Infrastructure.Attribute;
using ZR.Model;
using ZR.Model.System.Generate;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 代码生成表
    /// </summary>
    [AppService(ServiceType = typeof(IGenTableService), ServiceLifetime = LifeTime.Transient)]
    public class GenTableService : BaseService<GenTable>, IGenTableService
    {
        private IGenTableColumnService GenTableColumnService;
        public GenTableService(IGenTableColumnService genTableColumnService)
        {
            GenTableColumnService = genTableColumnService;
        }

        /// <summary>
        /// 删除表
        /// </summary>
        /// <param name="tableIds">需要删除的表id</param>
        /// <returns></returns>
        public int DeleteGenTableByIds(long[] tableIds)
        {
            Delete(f => tableIds.Contains(f.TableId));
            return GenTableColumnService.DeleteGenTableColumn(tableIds);
        }

        /// <summary>
        /// 删除表根据表名
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public int DeleteGenTableByTbName(string tableName)
        {
            return Delete(f => f.TableName == tableName) ? 1 : 0;
        }

        /// <summary>
        /// 获取表信息
        /// </summary>
        /// <param name="tableId"></param>
        /// <returns></returns>
        public GenTable GetGenTableInfo(long tableId)
        {
            GenTable info = GetId(tableId);
            if (info != null)
            {
                info.Columns = GenTableColumnService.GenTableColumns(tableId);
                if (!info.SubTableName.IsEmpty())
                {
                    info.SubTable = Queryable().Where(f => f.TableName == info.SubTableName).First();
                    info.SubTable.Columns = GenTableColumnService.GenTableColumns(info.SubTable.TableId);
                }
            }
            return info;
        }

        /// <summary>
        /// 获取所有代码生成表
        /// </summary>
        /// <returns></returns>
        public List<GenTable> GetGenTableAll()
        {
            return GetAll();
        }

        /// <summary>
        /// 查询代码生成表信息
        /// </summary>
        /// <param name="genTable"></param>
        /// <param name="pagerInfo"></param>
        /// <returns></returns>
        public PagedInfo<GenTable> GetGenTables(GenTable genTable, PagerInfo pagerInfo)
        {
            var predicate = Expressionable.Create<GenTable>();
            predicate = predicate.AndIF(genTable.TableName.IfNotEmpty(), it => it.TableName.Contains(genTable.TableName));

            return GetPages(predicate.ToExpression(), pagerInfo, x => x.TableId, OrderByType.Desc);
        }

        /// <summary>
        /// 插入代码生成表
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public int ImportGenTable(GenTable table)
        {
            table.Create_time = DateTime.Now;
            //导入前删除现有表
            //DeleteGenTableByIds(new long[] { table.TableId });
            DeleteGenTableByTbName(table.TableName);

            return Insertable(table).IgnoreColumns(ignoreNullColumn: true).ExecuteReturnIdentity();
        }

        /// <summary>
        /// 获取表数据
        /// </summary>
        /// <param name="tableNames"></param>
        /// <returns></returns>
        public List<GenTable> SelectDbTableListByNamess(string[] tableNames)
        {
            throw new NotImplementedException();
        }

        public int UpdateGenTable(GenTable genTable)
        {
            var db = Context;
            genTable.Update_time = db.GetDate();
            return db.Updateable(genTable).IgnoreColumns(ignoreAllNullColumns: true).ExecuteCommand();
        }

        /// <summary>
        /// 同步数据库
        /// </summary>
        /// <param name="tableId">表id</param>
        /// <param name="genTable"></param>
        /// <param name="dbTableColumns">数据库表最新列初始化信息集合</param>
        public bool SynchDb(long tableId, GenTable genTable, List<GenTableColumn> dbTableColumns)
        {
            List<string> tableColumnNames = genTable.Columns.Select(f => f.ColumnName).ToList();//老列明
            List<string> dbTableColumneNames = dbTableColumns.Select(f => f.ColumnName).ToList();//新列明

            List<GenTableColumn> insertColumns = new();
            List<GenTableColumn> updateColumns = new();
            foreach (var column in dbTableColumns)
            {
                if (!tableColumnNames.Contains(column.ColumnName))
                {
                    insertColumns.Add(column);
                }
                else
                {
                    GenTableColumn prevColumn = genTable.Columns.Find(f => f.ColumnName == column.ColumnName);
                    column.ColumnId = prevColumn.ColumnId;
                    column.IsEdit = prevColumn.IsEdit;
                    column.AutoFillType = prevColumn.AutoFillType;
                    column.Sort = prevColumn.Sort;
                    column.IsExport = prevColumn.IsExport;
                    column.IsSort = prevColumn.IsSort;
                    column.IsQuery = prevColumn.IsQuery;
                    column.IsList = prevColumn.IsList;
                    column.HtmlType = prevColumn.HtmlType;
                    column.Update_time = DateTime.Now;
                    column.Update_by = genTable.Update_by;

                    if (column.IsList)
                    {
                        column.DictType = prevColumn.DictType;
                        column.QueryType = prevColumn.QueryType;
                    }
                    //不同步列说明
                    //if (column.ColumnComment.IsEmpty())
                    //{
                    //    column.ColumnComment = prevColumn.ColumnComment;
                    //}
                    updateColumns.Add(column);
                }
            }
            var result = UseTran(() =>
            {
                if (insertColumns.Count > 0)
                {
                    GenTableColumnService.Insert(insertColumns);
                }
                if (updateColumns.Count > 0)
                {
                    GenTableColumnService.UpdateGenTableColumn(updateColumns);
                }

                List<GenTableColumn> delColumns = genTable.Columns.FindAll(column => !dbTableColumneNames.Contains(column.ColumnName));
                if (delColumns != null && delColumns.Count > 0)
                {
                    GenTableColumnService.Delete(delColumns.Select(f => f.ColumnId).ToList());
                }
            });
            return result.IsSuccess;
        }
    }

    /// <summary>
    /// 代码生成表列
    /// </summary>
    [AppService(ServiceType = typeof(IGenTableColumnService), ServiceLifetime = LifeTime.Transient)]
    public class GenTableColumnService : BaseService<GenTableColumn>, IGenTableColumnService
    {
        /// <summary>
        /// 删除表字段
        /// </summary>
        /// <param name="tableId"></param>
        /// <returns></returns>
        public int DeleteGenTableColumn(long tableId)
        {
            return Deleteable().Where(f => new long[] { tableId }.Contains(f.TableId)).ExecuteCommand();
        }

        /// <summary>
        /// 根据表id批量删除表字段
        /// </summary>
        /// <param name="tableId"></param>
        /// <returns></returns>
        public int DeleteGenTableColumn(long[] tableId)
        {
            return Deleteable().Where(f => tableId.Contains(f.TableId)).ExecuteCommand();
        }

        /// <summary>
        /// 根据表名删除字段
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public int DeleteGenTableColumnByTableName(string tableName)
        {
            return Deleteable().Where(f => f.TableName == tableName).ExecuteCommand();
        }

        /// <summary>
        /// 获取表所有字段
        /// </summary>
        /// <param name="tableId"></param>
        /// <returns></returns>
        public List<GenTableColumn> GenTableColumns(long tableId)
        {
            return Queryable().Where(f => f.TableId == tableId).OrderBy(x => x.Sort).ToList();
        }

        /// <summary>
        /// 插入表字段
        /// </summary>
        /// <param name="tableColumn"></param>
        /// <returns></returns>
        public int InsertGenTableColumn(List<GenTableColumn> tableColumn)
        {
            return Context.Insertable(tableColumn).IgnoreColumns(x => new { x.Remark }).ExecuteCommand();
        }

        /// <summary>
        /// 批量更新表字段
        /// </summary>
        /// <param name="tableColumn"></param>
        /// <returns></returns>
        public int UpdateGenTableColumn(List<GenTableColumn> tableColumn)
        {
            return Context.Updateable(tableColumn)
                .WhereColumns(it => new { it.ColumnId })
                .UpdateColumns(it => new
                {
                    it.ColumnComment,
                    it.CsharpField,
                    it.CsharpType,
                    it.IsQuery,
                    it.IsEdit,
                    it.IsInsert,
                    it.IsList,
                    it.QueryType,
                    it.HtmlType,
                    it.IsRequired,
                    it.Sort,
                    it.Update_time,
                    it.DictType,
                    it.Update_by,
                    it.Remark,
                    it.IsSort,//
                    it.IsExport,
                    it.AutoFillType,
                })
                .ExecuteCommand();
        }
    }
}
