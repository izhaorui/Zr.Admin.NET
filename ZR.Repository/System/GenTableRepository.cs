using Infrastructure.Attribute;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZR.Model.System.Generate;

namespace ZR.Repository.System
{
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class GenTableRepository : BaseRepository<GenTable>
    {

    }
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class GenTableColumnRepository : BaseRepository<GenTableColumn>
    {
        /// <summary>
        /// 根据表id批量删除表字段
        /// </summary>
        /// <param name="tableId"></param>
        /// <returns></returns>
        public int DeleteGenTableColumn(long[] tableId)
        {
            return Context.Deleteable<GenTableColumn>().Where(f => tableId.Contains(f.TableId)).ExecuteCommand();
        }

        /// <summary>
        /// 根据表名删除字段
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public int DeleteGenTableColumnByTableName(string tableName)
        {
            return Context.Deleteable<GenTableColumn>().Where(f => f.TableName == tableName).ExecuteCommand();
        }

        /// <summary>
        /// 获取表所有字段
        /// </summary>
        /// <param name="tableId"></param>
        /// <returns></returns>
        public List<GenTableColumn> GenTableColumns(long tableId)
        {
            return Context.Queryable<GenTableColumn>().Where(f => f.TableId == tableId).OrderBy(x => x.Sort).ToList();
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
                .WhereColumns(it => new { it.ColumnId, it.TableId})
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
                    it.IsSort
                })
                .ExecuteCommand();
        }
    }
}
