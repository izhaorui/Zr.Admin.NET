using Infrastructure.Attribute;
using SqlSugar;
using System.Collections.Generic;
using ZR.Model;
using ZR.Model.System;

namespace ZR.Repository.System
{
    /// <summary>
    /// 字典
    /// </summary>    
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class SysDictRepository : BaseRepository<SysDictType>
    {
        public List<SysDictType> GetAll()
        {
            return Context.Queryable<SysDictType>().ToList();
        }

        /// <summary>
        /// 查询字段类型列表
        /// </summary>
        /// <param name="dictType">实体模型</param>
        /// <returns></returns>
        public PagedInfo<SysDictType> SelectDictTypeList(SysDictType dictType, PagerInfo pager)
        {
            var exp = Expressionable.Create<SysDictType>();
            exp.AndIF(!string.IsNullOrEmpty(dictType.DictName), it => it.DictName.Contains(dictType.DictName));
            exp.AndIF(!string.IsNullOrEmpty(dictType.Status), it => it.Status == dictType.Status);
            exp.AndIF(!string.IsNullOrEmpty(dictType.DictType), it => it.DictType.Contains(dictType.DictType));
            exp.AndIF(!string.IsNullOrEmpty(dictType.Type), it => it.Type.Equals(dictType.Type));

            return GetPages(exp.ToExpression(), pager, f => f.DictId, OrderByType.Desc);
        }

        /// <summary>
        /// 批量删除
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public int DeleteDictTypeByIds(long[] id)
        {
            return Context.Deleteable<SysDictType>().In(id).ExecuteCommand();
        }

        /// <summary>
        /// 修改
        /// </summary>
        /// <param name="sysDictType"></param>
        /// <returns></returns>
        public int UpdateDictType(SysDictType dictType)
        {
            return Context.Updateable(dictType).IgnoreColumns(it => new { dictType.Create_by }).ExecuteCommand();
        }
    }
}
