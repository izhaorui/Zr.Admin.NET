using Infrastructure.Attribute;
using Infrastructure.Extensions;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using ZR.Common;
using ZR.Model;
using ZR.Model.System.Generate;
using ZR.Repository.System;
using ZR.Service.System.IService;

namespace ZR.Service.System
{
    /// <summary>
    /// 代码生成表
    /// </summary>
    [AppService(ServiceType = typeof(IGenTableService), ServiceLifetime = LifeTime.Transient)]
    public class GenTableService : BaseService<GenTable>, IGenTableService
    {
        private GenTableRepository GenTableRepository;
        private IGenTableColumnService GenTableColumnService;
        public GenTableService(IGenTableColumnService genTableColumnService, GenTableRepository genTableRepository)
        {
            GenTableColumnService = genTableColumnService;
            GenTableRepository = genTableRepository;
        }

        /// <summary>
        /// 删除表
        /// </summary>
        /// <param name="tableIds">需要删除的表id</param>
        /// <returns></returns>
        public int DeleteGenTableByIds(long[] tableIds)
        {
            GenTableRepository.Delete(f => tableIds.Contains(f.TableId));
            return GenTableColumnService.DeleteGenTableColumn(tableIds);
        }

        /// <summary>
        /// 删除表根据表名
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public int DeleteGenTableByTbName(string tableName)
        {
            return GenTableRepository.Delete(f => f.TableName == tableName) ? 1 : 0;
        }

        /// <summary>
        /// 获取表信息
        /// </summary>
        /// <param name="tableId"></param>
        /// <returns></returns>
        public GenTable GetGenTableInfo(long tableId)
        {
            var info = GenTableRepository.GetId(tableId);
            SetTableFromOptions(info);
            if (info != null && !info.SubTableName.IsEmpty())
            {
                info.SubTable = GenTableRepository.Queryable().Where(f => f.SubTableName == info.TableName).First();
                if (info?.SubTable != null)
                {
                    SetTableFromOptions(info?.SubTable);
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
            return GenTableRepository.GetAll();
        }

        /// <summary>
        /// 设置代码生成其他参数
        /// </summary>
        /// <param name="genTable"></param>
        private void SetTableFromOptions(GenTable genTable)
        {
            //附加参数，key，value格式
            if (!string.IsNullOrEmpty(genTable?.Options))
            {
                Dictionary<string, object> options = JsonConvert.DeserializeObject<Dictionary<string, object>>(genTable.Options);
                if (options == null) return;

                genTable.ParentMenuId = options.GetValueOrDefault("parentMenuId") ?? null;

                genTable.SortType = options.GetValueOrDefault("sortType") ?? "asc";
                genTable.SortField = options.GetValueOrDefault("sortField") ?? "";

                genTable.TreeParentCode = options.GetValueOrDefault("treeParentCode") ?? "";
                genTable.TreeName = options.GetValueOrDefault("treeName") ?? "";
                genTable.TreeCode = options.GetValueOrDefault("treeCode") ?? "";

                var checkdBtn = options.GetValueOrDefault("checkedBtn") ?? "";
                genTable.CheckedBtn = Tools.SpitIntArrary(checkdBtn.ToString());

                var permPrefix = options.GetValueOrDefault("permissionPrefix", "");

                genTable.PermissionPrefix = permPrefix?.ToString();
            }
            if (genTable.PermissionPrefix.IsEmpty())
            {
                genTable.PermissionPrefix = $"{genTable.ModuleName.ToLower()}:{genTable.ClassName.ToLower()}";//权限
            }
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

            return GenTableRepository.GetPages(predicate.ToExpression(), pagerInfo, x => x.TableId, OrderByType.Desc);
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

            return GenTableRepository.Context.Insertable(table).IgnoreColumns(ignoreNullColumn: true).ExecuteReturnIdentity();
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
            var db = GenTableRepository.Context;
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
            GenTableColumnService.Insert(insertColumns);

            List<GenTableColumn> delColumns = tableColumns.FindAll(column => !dbTableColumneNames.Contains(column.ColumnName));
            if (delColumns != null && delColumns.Count > 0)
            {
                GenTableColumnService.Delete(delColumns.Select(f => f.ColumnId).ToList());
            }
        }
    }

    /// <summary>
    /// 代码生成表列
    /// </summary>
    [AppService(ServiceType = typeof(IGenTableColumnService), ServiceLifetime = LifeTime.Transient)]
    public class GenTableColumnService : BaseService<GenTableColumn>, IGenTableColumnService
    {

        private GenTableColumnRepository GetTableColumnRepository;
        public GenTableColumnService(GenTableColumnRepository genTableColumnRepository)
        {
            GetTableColumnRepository = genTableColumnRepository;
        }
        /// <summary>
        /// 删除表字段
        /// </summary>
        /// <param name="tableColumn"></param>
        /// <returns></returns>
        public int DeleteGenTableColumn(long tableId)
        {
            return GetTableColumnRepository.DeleteGenTableColumn(new long[] { tableId });
        }
        /// <summary>
        /// 根据表id批量删除表字段
        /// </summary>
        /// <param name="tableId"></param>
        /// <returns></returns>
        public int DeleteGenTableColumn(long[] tableId)
        {
            return GetTableColumnRepository.DeleteGenTableColumn(tableId);
        }

        /// <summary>
        /// 根据表名删除字段
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public int DeleteGenTableColumnByTableName(string tableName)
        {
            return GetTableColumnRepository.DeleteGenTableColumnByTableName(tableName);
        }

        /// <summary>
        /// 获取表所有字段
        /// </summary>
        /// <param name="tableId"></param>
        /// <returns></returns>
        public List<GenTableColumn> GenTableColumns(long tableId)
        {
            return GetTableColumnRepository.GenTableColumns(tableId);
        }

        /// <summary>
        /// 插入表字段
        /// </summary>
        /// <param name="tableColumn"></param>
        /// <returns></returns>
        public int InsertGenTableColumn(List<GenTableColumn> tableColumn)
        {
            return GetTableColumnRepository.InsertGenTableColumn(tableColumn);
        }

        /// <summary>
        /// 批量更新表字段
        /// </summary>
        /// <param name="tableColumn"></param>
        /// <returns></returns>
        public int UpdateGenTableColumn(List<GenTableColumn> tableColumn)
        {
            return GetTableColumnRepository.UpdateGenTableColumn(tableColumn);
        }
    }
}
