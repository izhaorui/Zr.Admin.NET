using Infrastructure.Attribute;
using Infrastructure.Model;
using System;
using System.Collections.Generic;
using System.Text;
using ZR.Common;
using ZR.Model;
using ZR.Model.System;
using ZR.Repository.System;
using ZR.Service.System.IService;

namespace ZR.Service.System
{
    /// <summary>
    /// 字典数据类
    /// </summary>
    [AppService(ServiceType = typeof(ISysDictDataService), ServiceLifetime = LifeTime.Transient)]
    public class SysDictDataService : BaseService<SysDictData>, ISysDictDataService
    {

        private readonly SysDictDataRepository SysDictDataRepository;
        public SysDictDataService(SysDictDataRepository sysDictDataRepository)
        {
            SysDictDataRepository = sysDictDataRepository;
        }

        /// <summary>
        /// 查询字典数据
        /// </summary>
        /// <param name="dictData"></param>
        /// <returns></returns>
        public PagedInfo<SysDictData> SelectDictDataList(SysDictData dictData, PagerInfo pagerInfo)
        {
            return SysDictDataRepository.SelectDictDataList(dictData, pagerInfo);
        }

        /// <summary>
        /// 根据字典类型查询
        /// </summary>
        /// <param name="dictType"></param>
        /// <returns></returns>
        public List<SysDictData> SelectDictDataByType(string dictType)
        {
            string CK = $"SelectDictDataByType_{dictType}";
            if (CacheHelper.GetCache(CK) is not List<SysDictData> list)
            {
                list = SysDictDataRepository.SelectDictDataByType(dictType);
                CacheHelper.SetCache(CK, list, 30);
            }
            return list;
        }
        public List<SysDictData> SelectDictDataByTypes(string[] dictTypes)
        {
            string CK = $"SelectDictDataByTypes_{dictTypes}";
            if (CacheHelper.GetCache(CK) is not List<SysDictData> list)
            {
                list = SysDictDataRepository.SelectDictDataByTypes(dictTypes);
                //CacheHelper.SetCache(CK, list, 30);
            }
            return list;
        }
        /// <summary>
        /// 根据字典数据ID查询信息
        /// </summary>
        /// <param name="dictCode"></param>
        /// <returns></returns>
        public SysDictData SelectDictDataById(long dictCode)
        {
            string CK = $"SelectDictDataByCode_{dictCode}";
            if (CacheHelper.GetCache(CK) is not SysDictData list)
            {
                list = SysDictDataRepository.GetFirst(f => f.DictCode == dictCode);
                CacheHelper.SetCache(CK, list, 5);
            }
            return list;
        }

        /// <summary>
        /// 插入数据
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        public long InsertDictData(SysDictData dict)
        {
            return SysDictDataRepository.InsertDictData(dict);
        }

        /// <summary>
        /// 修改数据
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        public long UpdateDictData(SysDictData dict)
        {
            var result = SysDictDataRepository.UpdateDictData(dict);
            CacheHelper.Remove($"SelectDictDataByCode_{dict.DictCode}");
            return result;
        }

        /// <summary>
        /// 批量删除字典数据信息
        /// </summary>
        /// <param name="dictCodes"></param>
        /// <returns></returns>
        public int DeleteDictDataByIds(long[] dictCodes)
        {
            return SysDictDataRepository.DeleteDictDataByIds(dictCodes);
        }
    }
}
