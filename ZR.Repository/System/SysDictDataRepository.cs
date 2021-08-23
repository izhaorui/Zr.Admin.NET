using Infrastructure.Attribute;
using System;
using System.Collections.Generic;
using ZR.Model.System;

namespace ZR.Repository.System
{
    /// <summary>
    /// 字典数据
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class SysDictDataRepository : BaseRepository
    {
        /// <summary>
        /// 字典类型数据搜索
        /// </summary>
        /// <param name="dictData"></param>
        /// <returns></returns>
        public List<SysDictData> SelectDictDataList(SysDictData dictData)
        {
            return Db.Queryable<SysDictData>()
                .WhereIF(!string.IsNullOrEmpty(dictData.DictLabel), it => it.DictLabel.Contains(dictData.DictLabel))
                .WhereIF(!string.IsNullOrEmpty(dictData.Status), it => it.Status == dictData.Status)
                .WhereIF(!string.IsNullOrEmpty(dictData.DictType), it => it.DictType == dictData.DictType)
                .ToList();
        }

        /// <summary>
        /// 根据字典类型查询
        /// </summary>
        /// <param name="dictData"></param>
        /// <returns></returns>
        public List<SysDictData> SelectDictDataByType(string dictType)
        {
            return Db.Queryable<SysDictData>().Where(f => f.Status == "0" && f.DictType == dictType)
                .OrderBy(it => it.DictSort)
                .ToList();
        }

        /// <summary>
        /// 根据DictCode查询
        /// </summary>
        /// <param name="dictCode"></param>
        /// <returns></returns>
        public SysDictData SelectDictDataById(long dictCode)
        {
            return Db.Queryable<SysDictData>().Where(f => f.DictCode == dictCode).First();
        }

        /// <summary>
        /// 新增保存字典数据信息
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        public long InsertDictData(SysDictData dict)
        {
            var result = Db.Insertable(dict).IgnoreColumns(it => new { dict.Update_by })
                .ExecuteReturnIdentity();
            return result;
        }

        /// <summary>
        /// 修改数据
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        public long UpdateDictData(SysDictData dict)
        {
            return Db.Updateable<SysDictData>()
                .SetColumns(t => new SysDictData()
                {
                    Remark = dict.Remark,
                    Update_time = DateTime.Now,
                    DictSort = dict.DictSort,
                    DictLabel = dict.DictLabel,
                    DictValue = dict.DictValue,
                    Status = dict.Status
                })
                .Where(f => f.DictCode == dict.DictCode).ExecuteCommand();
        }

        /// <summary>
        /// 批量删除字典数据信息
        /// </summary>
        /// <param name="dictCodes"></param>
        /// <returns></returns>
        public int DeleteDictDataByIds(long[] dictCodes)
        {
            return Db.Deleteable<SysDictData>().In(dictCodes).ExecuteCommand();
        }

        /// <summary>
        /// 查询字典数据
        /// </summary>
        /// <param name="dictType"></param>
        /// <returns></returns>
        public int CountDictDataByType(string dictType)
        {
            return Db.Queryable<SysDictData>().Count(f => f.DictType == dictType);
        }

        /// <summary>
        /// 同步修改字典类型
        /// </summary>
        /// <param name="old_dictType">旧字典类型</param>
        /// <param name="new_dictType">新字典类型</param>
        /// <returns></returns>
        public int UpdateDictDataType(string old_dictType, string new_dictType)
        {
            //只更新DictType字段根据where条件
            return Db.Updateable<SysDictData>()
                .SetColumns(t => new SysDictData() { DictType = new_dictType })
                .Where(f => f.DictType == old_dictType)
                .ExecuteCommand();
        }
    }
}
