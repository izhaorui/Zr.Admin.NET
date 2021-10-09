using Infrastructure.Attribute;
using System;
using System.Collections.Generic;
using System.Text;
using ZR.Common;
using ZR.Model.System;
using ZR.Repository.System;
using ZR.Service.System.IService;

namespace ZR.Service.System
{
    [AppService(ServiceType = typeof(ISysDictDataService), ServiceLifetime = LifeTime.Transient)]
    public class SysDictDataService: ISysDictDataService
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
        public List<SysDictData> SelectDictDataList(SysDictData dictData)
        {
            return SysDictDataRepository.SelectDictDataList(dictData);
        }

        /// <summary>
        /// 根据字典类型查询
        /// </summary>
        /// <param name="dictData"></param>
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
