using Infrastructure;
using Infrastructure.Attribute;
using System.Collections.Generic;
using System.Text;
using ZR.Model;
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
        private SysDictRepository DictRepository;
        private SysDictDataRepository DictDataRepository;

        public SysDictService(SysDictRepository sysDictRepository, SysDictDataRepository dictDataRepository)
        {
            this.DictRepository = sysDictRepository;
            this.DictDataRepository = dictDataRepository;
        }
        public List<SysDictType> GetAll()
        {
            return DictRepository.GetAll();
        }

        /// <summary>
        /// 查询字段类型列表
        /// </summary>
        /// <param name="dictType">实体模型</param>
        /// <returns></returns>
        public PagedInfo<SysDictType> SelectDictTypeList(SysDictType dictType, Model.PagerInfo pager)
        {
            return DictRepository.SelectDictTypeList(dictType, pager);
        }

        /// <summary>
        /// 校验字典类型称是否唯一
        /// </summary>
        /// <param name="dictType">字典类型</param>
        /// <returns></returns>
        public string CheckDictTypeUnique(SysDictType dictType)
        {
            SysDictType sysDictType = DictRepository.GetFirst(f => f.DictType == dictType.DictType);
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
                SysDictType dictType = DictRepository.GetFirst(x => x.DictId == dictId);
                if (DictDataRepository.Count(f => f.DictType == dictType.DictType) > 0)
                {
                    throw new CustomException($"{dictType.DictName}已分配,不能删除");
                }
            }
            int count = DictRepository.DeleteDictTypeByIds(dictIds);
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
            return DictRepository.InsertReturnBigIdentity(sysDictType);
        }

        /// <summary>
        /// 修改字典类型
        /// </summary>
        /// <param name="sysDictType"></param>
        /// <returns></returns>
        public int UpdateDictType(SysDictType sysDictType)
        {
            SysDictType oldDict = DictRepository.GetFirst(x => x.DictId == sysDictType.DictId);
            if (sysDictType.DictType != oldDict.DictType)
            {
                //同步修改 dict_data表里面的DictType值
                DictDataRepository.UpdateDictDataType(oldDict.DictType, sysDictType.DictType);
            }
            return DictRepository.UpdateDictType(sysDictType);
        }

        /// <summary>
        /// 获取字典信息
        /// </summary>
        /// <param name="dictId"></param>
        /// <returns></returns>
        public SysDictType GetInfo(long dictId)
        {
            return DictRepository.GetFirst(f => f.DictId == dictId);
        }
    }
}
