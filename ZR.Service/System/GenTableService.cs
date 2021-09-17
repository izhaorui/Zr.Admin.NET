using Infrastructure.Attribute;
using Infrastructure.Extensions;
using Infrastructure.Model;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        /// <summary>
        /// 删除表
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public int DeleteGenTable(GenTable table)
        {
            return Db.Deleteable<GenTable>().Where(f => f.TableName == table.TableName).ExecuteCommand();
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
        public int InsertGenTable(GenTable table)
        {
            var db = Db;
            DeleteGenTable(table);
            return db.Insertable(table).ExecuteReturnIdentity();
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
            return Db.Deleteable<GenTableColumn>().Where(f => f.TableId == tableId).ExecuteCommand();
        }

        /// <summary>
        /// 获取表所有字段
        /// </summary>
        /// <param name="tableId"></param>
        /// <returns></returns>
        public List<GenTableColumn> GenTableColumns(long tableId)
        {
            return GetAll().OrderBy(x => x.Sort).ToList();
        }

        /// <summary>
        /// 插入表字段
        /// </summary>
        /// <param name="tableColumn"></param>
        /// <returns></returns>
        public int InsertGenTableColumn(List<GenTableColumn> tableColumn)
        {
            return Db.Insertable(tableColumn).ExecuteCommand();
        }
    }
}
