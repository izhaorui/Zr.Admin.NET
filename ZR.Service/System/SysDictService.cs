using Infrastructure;
using Infrastructure.Attribute;
using System;
using System.Collections.Generic;
using System.Text;
using ZR.Model.System;
using ZR.Repository.System;
using ZR.Service.System.IService;

namespace ZR.Service.System
{
    /// <summary>
    /// 字典类型
    /// </summary>
    [AppService(ServiceType = typeof(ISysDictService), ServiceLifetime = LifeTime.Transient)]
    public class SysDictService : BaseService<SysDictType>, ISysDictService
    {
        private SysDictRepository sysDictRepository;
        private SysDictDataRepository DictDataRepository;

        public SysDictService(SysDictRepository sysDictRepository, SysDictDataRepository dictDataRepository)
        {
            this.sysDictRepository = sysDictRepository;
            this.DictDataRepository = dictDataRepository;
        }

        /// <summary>
        /// 查询字段类型列表
        /// </summary>
        /// <param name="dictType">实体模型</param>
        /// <returns></returns>
        public List<SysDictType> SelectDictTypeList(SysDictType dictType, Model.PagerInfo pager)
        {
            return sysDictRepository.SelectDictTypeList(dictType, pager);
        }

        /// <summary>
        /// 校验字典类型称是否唯一
        /// </summary>
        /// <param name="dictType">字典类型</param>
        /// <returns></returns>
        public string CheckDictTypeUnique(SysDictType dictType)
        {
            SysDictType sysDictType = GetFirst(f => f.DictType == dictType.DictType);
            if (sysDictType != null && sysDictType.DictId != dictType.DictId)
            {
                return UserConstants.NOT_UNIQUE;
            }
            return UserConstants.UNIQUE;
        }

        /// <summary>
        /// 批量删除字典数据信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public int DeleteDictTypeByIds(long[] dictIds)
        {
            foreach (var dictId in dictIds)
            {
                SysDictType dictType = GetFirst(x => x.DictId == dictId);
                if (GetCount(f => f.DictType == dictType.DictType) > 0)
                {
                    throw new CustomException($"{dictType.DictName}已分配,不能删除");
                }
            }
            int count = sysDictRepository.DeleteDictTypeByIds(dictIds);
            //if (count > 0)
            //{
            //    DictUtils.clearDictCache();
            //}
            return count;
        }

        /// <summary>
        /// 插入字典类型
        /// </summary>
        /// <param name="sysDictType"></param>
        /// <returns></returns>
        public long InsertDictType(SysDictType sysDictType)
        {
            return Saveable(sysDictType, iColumns: it => new { sysDictType.Update_by }).DictId;
        }

        /// <summary>
        /// 修改字典类型
        /// </summary>
        /// <param name="sysDictType"></param>
        /// <returns></returns>
        public int UpdateDictType(SysDictType sysDictType)
        {
            SysDictType oldDict = GetFirst(x => x.DictId == sysDictType.DictId);
            if (sysDictType.DictType != oldDict.DictType)
            {
                //同步修改 dict_data表里面的DictType值
                DictDataRepository.UpdateDictDataType(oldDict.DictType, sysDictType.DictType);
            }
            return sysDictRepository.UpdateDictType(sysDictType);
        }
    }
}
