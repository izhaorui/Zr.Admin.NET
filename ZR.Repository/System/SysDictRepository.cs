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
    public class SysDictRepository : BaseRepository
    {
        /// <summary>
        /// 查询字段类型列表
        /// </summary>
        /// <param name="dictType">实体模型</param>
        /// <returns></returns>
        public List<SysDictType> SelectDictTypeList(SysDictType dictType)
        {
            return Db
                .Queryable<SysDictType>()
                .WhereIF(!string.IsNullOrEmpty(dictType.DictName), it => it.DictName.Contains(dictType.DictName))
                .WhereIF(!string.IsNullOrEmpty(dictType.Status), it => it.Status == dictType.Status)
                .WhereIF(!string.IsNullOrEmpty(dictType.DictType), it => it.DictType == dictType.DictType).ToList();
        }

        /// <summary>
        /// 根据Id查询
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public SysDictType SelectDictTypeById(long id)
        {
            return Db.Queryable<SysDictType>().First(it => it.DictId == id);
        }

        /// <summary>
        /// 检查字典类型唯一值
        /// </summary>
        /// <param name="dictType"></param>
        /// <returns></returns>
        public SysDictType CheckDictTypeUnique(string dictType)
        {
            return Db.Queryable<SysDictType>().First(it => it.DictType == dictType);
        }

        /// <summary>
        /// 删除一个
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public int DeleteDictTypeById(long id)
        {
            return Db.Deleteable<SysDictType>().In(id).ExecuteCommand();
        }

        /// <summary>
        /// 批量删除
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public int DeleteDictTypeByIds(long[] id)
        {
            return Db.Deleteable<SysDictType>().In(id).ExecuteCommand();
        }

        /// <summary>
        /// 插入
        /// </summary>
        /// <param name="sysDictType"></param>
        /// <returns></returns>
        public long InsertDictType(SysDictType sysDictType)
        {
            var result = Db.Insertable(sysDictType).IgnoreColumns(it => new { sysDictType.Update_by })
                .ExecuteReturnIdentity();
            return result;
        }

        /// <summary>
        /// 修改
        /// </summary>
        /// <param name="sysDictType"></param>
        /// <returns></returns>
        public int UpdateDictType(SysDictType dictType)
        {
            return Db.Updateable(dictType).IgnoreColumns(it => new { dictType.Create_by }).ExecuteCommand();
        }
    }
}
