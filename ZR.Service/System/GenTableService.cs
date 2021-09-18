using Infrastructure.Attribute;
using Infrastructure.Extensions;
using Infrastructure.Model;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public IGenTableColumnService GenTableColumnService;
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
            Db.Deleteable<GenTable>().Where(f => tableIds.Contains(f.TableId)).ExecuteCommand();
            return GenTableColumnService.DeleteGenTableColumn(tableIds);
        }

        /// <summary>
        /// 删除表根据表名
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public int DeleteGenTableByTbName(string tableName)
        {
            return Db.Deleteable<GenTable>().Where(f => f.TableName == tableName).ExecuteCommand();
        }

        /// <summary>
        /// 获取表信息
        /// </summary>
        /// <param name="tableId"></param>
        /// <returns></returns>
        public GenTable GetGenTableInfo(long tableId)
        {
            return GetId(tableId);
        }

        /// <summary>
        /// 查询代码生成表信息
        /// </summary>
        /// <param name="genTable"></param>
        /// <param name="pagerInfo"></param>
        /// <returns></returns>
        public PagedInfo<GenTable> GetGenTables(GenTable genTable, Model.PagerInfo pagerInfo)
        {
            var predicate = Expressionable.Create<GenTable>();
            predicate = predicate.AndIF(genTable.TableName.IfNotEmpty(), it => it.TableName.Contains(genTable.TableName));

            return GetPages(predicate.ToExpression(), pagerInfo);
        }

        /// <summary>
        /// 插入代码生成表
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public int ImportGenTable(GenTable table)
        {
            var db = Db;
            table.Create_time = db.GetDate();
            //导入前删除现有表
            //DeleteGenTableByIds(new long[] { table.TableId });
            DeleteGenTableByTbName(table.TableName);

            return db.Insertable(table).IgnoreColumns(ignoreNullColumn: true).ExecuteReturnIdentity();
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
            var db = Db;
            genTable.Update_time = db.GetDate();
            return db.Updateable(genTable).IgnoreColumns(ignoreAllNullColumns: true).ExecuteCommand();
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
            return DeleteGenTableColumn(new long[] { tableId });
        }
        /// <summary>
        /// 根据表id批量删除表字段
        /// </summary>
        /// <param name="tableId"></param>
        /// <returns></returns>
        public int DeleteGenTableColumn(long[] tableId)
        {
            return Db.Deleteable<GenTableColumn>().Where(f => tableId.Contains(f.TableId)).ExecuteCommand();
        }

        /// <summary>
        /// 根据表名删除字段
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public int DeleteGenTableColumnByTableName(string tableName)
        {
            return Db.Deleteable<GenTableColumn>().Where(f => f.TableName == tableName).ExecuteCommand();
        }

        /// <summary>
        /// 获取表所有字段
        /// </summary>
        /// <param name="tableId"></param>
        /// <returns></returns>
        public List<GenTableColumn> GenTableColumns(long tableId)
        {
            return Db.Queryable<GenTableColumn>().Where(f => f.TableId == tableId).OrderBy(x => x.Sort).ToList();
        }

        /// <summary>
        /// 插入表字段
        /// </summary>
        /// <param name="tableColumn"></param>
        /// <returns></returns>
        public int InsertGenTableColumn(List<GenTableColumn> tableColumn)
        {
            return Db.Insertable(tableColumn).IgnoreColumns(x => new { x.Remark}).ExecuteCommand();
        }

        /// <summary>
        /// 批量更新表字段
        /// </summary>
        /// <param name="tableColumn"></param>
        /// <returns></returns>
        public int UpdateGenTableColumn(List<GenTableColumn> tableColumn)
        {
            return Db.Updateable(tableColumn).IgnoreColumns(x => new { x.Remark }).ExecuteCommand();
        }
    }
}
