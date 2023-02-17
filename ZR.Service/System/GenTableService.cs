using Infrastructure.Attribute;
using Infrastructure.Extensions;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using ZR.Model;
using ZR.Model.System.Generate;
using ZR.Service.System.IService;

namespace ZR.Service.System
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
            if (info != null && !info.SubTableName.IsEmpty())
            {
                info.SubTable = Queryable().Where(f => f.TableName == info.SubTableName).First();
                info.SubTable.Columns = GenTableColumnService.GenTableColumns(info.SubTable.TableId);
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
        /// <param name="dbTableColumns"></param>
        /// <param name="genTable"></param>
        public void SynchDb(long tableId, GenTable genTable, List<GenTableColumn> dbTableColumns)
        {
            List<GenTableColumn> tableColumns = GenTableColumnService.GenTableColumns(tableId);
            List<string> tableColumnNames = tableColumns.Select(f => f.ColumnName).ToList();
            List<string> dbTableColumneNames = dbTableColumns.Select(f => f.ColumnName).ToList();

            List<GenTableColumn> insertColumns = new();
            foreach (var column in dbTableColumns)
            {
                if (!tableColumnNames.Contains(column.ColumnName))
                {
                    insertColumns.Add(column);
                }
            }
            bool result = UseTran2(() =>
            {
                GenTableColumnService.Insert(insertColumns);

                List<GenTableColumn> delColumns = tableColumns.FindAll(column => !dbTableColumneNames.Contains(column.ColumnName));
                if (delColumns != null && delColumns.Count > 0)
                {
                    GenTableColumnService.Delete(delColumns.Select(f => f.ColumnId).ToList());
                }
            });
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
        /// <param name="tableColumn"></param>
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
                .WhereColumns(it => new { it.ColumnId, it.TableId })
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
                    it.IsSort,
                    it.IsExport,
                    it.AutoFillType
                })
                .ExecuteCommand();
        }
    }
}
