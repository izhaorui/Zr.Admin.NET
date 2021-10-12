using Infrastructure.Attribute;
using System;
using System.Collections.Generic;
using System.Text;
using ZR.Model.System;

namespace ZR.Repository.System
{
    /// <summary>
    /// 字典
    /// </summary>    
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class SysDictRepository : BaseRepository<SysDictType>
    {
        /// <summary>
        /// 查询字段类型列表
        /// </summary>
        /// <param name="dictType">实体模型</param>
        /// <returns></returns>
        public List<SysDictType> SelectDictTypeList(SysDictType dictType, Model.PagerInfo pager)
        {
            var totalNum = 0;
            var list = Context
                .Queryable<SysDictType>()
                .WhereIF(!string.IsNullOrEmpty(dictType.DictName), it => it.DictName.Contains(dictType.DictName))
                .WhereIF(!string.IsNullOrEmpty(dictType.Status), it => it.Status == dictType.Status)
                .WhereIF(!string.IsNullOrEmpty(dictType.DictType), it => it.DictType == dictType.DictType)
                .ToPageList(pager.PageNum, pager.PageSize, ref totalNum);
            pager.TotalNum = totalNum;
            return list;
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
