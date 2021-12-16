using Infrastructure.Attribute;
using Infrastructure.Model;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using ZR.Model;
using ZR.Model.System;

namespace ZR.Repository.System
{
    /// <summary>
    /// 字典数据
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class SysDictDataRepository : BaseRepository<SysDictData>
    {
        /// <summary>
        /// 字典类型数据搜索
        /// </summary>
        /// <param name="dictData"></param>
        /// <param name="pagerInfo"></param>
        /// <returns></returns>
        public PagedInfo<SysDictData> SelectDictDataList(SysDictData dictData, PagerInfo pagerInfo)
        {
            var exp = Expressionable.Create<SysDictData>();
            exp.AndIF(!string.IsNullOrEmpty(dictData.DictLabel), it => it.DictLabel.Contains(dictData.DictLabel));
            exp.AndIF(!string.IsNullOrEmpty(dictData.Status), it => it.Status == dictData.Status);
            exp.AndIF(!string.IsNullOrEmpty(dictData.DictType), it => it.DictType == dictData.DictType);
            return GetPages(exp.ToExpression(), pagerInfo);
        }

        /// <summary>
        /// 根据字典类型查询
        /// </summary>
        /// <param name="dictData"></param>
        /// <returns></returns>
        public List<SysDictData> SelectDictDataByType(string dictType)
        {
            return Context.Queryable<SysDictData>().Where(f => f.Status == "0" && f.DictType == dictType)
                .OrderBy(it => it.DictSort)
                .ToList();
        }

        /// <summary>
        /// 根据字典类型查询
        /// </summary>
        /// <param name="dictData"></param>
        /// <returns></returns>
        public List<SysDictData> SelectDictDataByTypes(string[] dictTypes)
        {
            return Context.Queryable<SysDictData>().Where(f => f.Status == "0" && dictTypes.Contains(f.DictType))
                .OrderBy(it => it.DictSort)
                .ToList();
        }
        /// <summary>
        /// 新增保存字典数据信息
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        public long InsertDictData(SysDictData dict)
        {
            var result = Context.Insertable(dict).ExecuteReturnBigIdentity();
            return result;
        }

        /// <summary>
        /// 修改数据
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        public long UpdateDictData(SysDictData dict)
        {
            return Context.Updateable<SysDictData>()
                .SetColumns(t => new SysDictData()
                {
                    Remark = dict.Remark,
                    Update_time = DateTime.Now,
                    DictSort = dict.DictSort,
                    DictLabel = dict.DictLabel,
                    DictValue = dict.DictValue,
                    Status = dict.Status,
                    CssClass = dict.CssClass,
                    ListClass = dict.ListClass
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
            return Delete(dictCodes);
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
            return Context.Updateable<SysDictData>()
                .SetColumns(t => new SysDictData() { DictType = new_dictType })
                .Where(f => f.DictType == old_dictType)
                .ExecuteCommand();
        }
    }
}
