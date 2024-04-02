using Infrastructure;
using Infrastructure.Attribute;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 字典类型
    /// </summary>
    [AppService(ServiceType = typeof(ISysDictService), ServiceLifetime = LifeTime.Transient)]
    public class SysDictService : BaseService<SysDictType>, ISysDictService
    {
        private ISysDictDataService DictDataService;

        public SysDictService(ISysDictDataService dictDataRepository)
        {
            this.DictDataService = dictDataRepository;
        }
        public List<SysDictType> GetAll()
        {
            return Queryable().ToList();
        }

        /// <summary>
        /// 查询字段类型列表
        /// </summary>
        /// <param name="dictType">实体模型</param>
        /// <param name="pager"></param>
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
        /// <param name="dictIds"></param>
        /// <returns></returns>
        public int DeleteDictTypeByIds(long[] dictIds)
        {
            int sysCount = Count(s => s.Type == "Y" && dictIds.Contains(s.DictId));
            if (sysCount > 0) { throw new CustomException($"删除失败Id： 系统内置参数不能删除！"); }
            foreach (var dictId in dictIds)
            {
                SysDictType dictType = GetFirst(x => x.DictId == dictId);
                if (DictDataService.Count(f => f.DictType == dictType.DictType) > 0)
                {
                    throw new CustomException($"{dictType.DictName}已分配,不能删除");
                }
            }
            int count = Context.Deleteable<SysDictType>().In(dictIds).ExecuteCommand();
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
            return InsertReturnBigIdentity(sysDictType);
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
                DictDataService.UpdateDictDataType(oldDict.DictType, sysDictType.DictType);
            }
            return Context.Updateable(sysDictType).IgnoreColumns(it => new { sysDictType.Create_by }).ExecuteCommand();
        }

        /// <summary>
        /// 获取字典信息
        /// </summary>
        /// <param name="dictId"></param>
        /// <returns></returns>
        public SysDictType GetInfo(long dictId)
        {
            return GetFirst(f => f.DictId == dictId);
        }

        /// <summary>
        /// 根据字典类型查询自定义sql
        /// </summary>
        /// <param name="dictType"></param>
        /// <returns></returns>
        public List<SysDictDataDto> SelectDictDataByCustomSql(string dictType)
        {
            var dictInfo = Queryable()
                .Where(f => f.DictType == dictType).First();
            if (dictInfo == null || !dictInfo.CustomSql.StartsWith("select", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return DictDataService.SelectDictDataByCustomSql(dictInfo);
        }
    }
}
