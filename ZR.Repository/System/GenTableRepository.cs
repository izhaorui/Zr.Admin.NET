using Infrastructure.Attribute;
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
            foreach (var item in tableColumn)
            {
                Context.Updateable<GenTableColumn>()
                    .Where(f => f.TableId == item.TableId)
                    .SetColumns(it => new GenTableColumn()
                    {
                        ColumnComment = item.ColumnComment,
                        CsharpField = item.CsharpField,
                        CsharpType = item.CsharpType,
                        IsQuery = item.IsQuery,
                        IsEdit = item.IsEdit,
                        IsInsert = item.IsInsert,
                        IsList = item.IsList,
                        QueryType = item.QueryType,
                        HtmlType = item.HtmlType,
                        IsRequired = item.IsRequired,
                        Sort = item.Sort,
                        Update_time = DateTime.Now,
                        DictType = item.DictType
                    })
                    .Where(f => f.ColumnId == item.ColumnId)
                    .ExecuteCommand();
            }

            return 1;
        }
    }
}
