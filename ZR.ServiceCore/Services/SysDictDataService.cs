using Infrastructure;
using Infrastructure.Attribute;
using SqlSugar.IOC;
using ZR.Common;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Model.System.Tenant;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 字典数据服务（多租户兼容）
    /// 主租户/非多租户：直接操作主库 SysDictData
    /// 普通租户：读时合并主库 + 租户扩展表，写时路由到租户扩展表。
    /// </summary>
    [AppService(ServiceType = typeof(ISysDictDataService), ServiceLifetime = LifeTime.Transient)]
    public class SysDictDataService : BaseService<SysDictData>, ISysDictDataService
    {
        #region 多租户辅助方法

        /// <summary>
        /// 缓存过期时间（分钟）
        /// </summary>
        private const int CACHE_EXPIRE_MINUTES = 10;

        /// <summary>
        /// 缓存 Key 前缀，按当前租户隔离
        /// </summary>
        private static string TCacheKey(string suffix) => $"{App.GetCurrentTenantId()}:{suffix}";

        /// <summary>
        /// 主库+字典值组合 Key，用于合并时快速查找
        /// </summary>
        private static string GetDictKey(string dictType, string dictValue) => $"{dictType}|{dictValue}";

        /// <summary>
        /// 获取租户库连接
        /// </summary>
        private ISqlSugarClient TenantDb()
        {
            if (!App.IsTenantEnabled()) return Context;
            var tenantId = App.GetCurrentTenantId();
            return DbScoped.SugarScope.GetConnectionScope(tenantId);
        }

        /// <summary>
        /// 获取主库连接
        /// </summary>
        private ISqlSugarClient MainDb()
        {
            if (!App.IsTenantEnabled()) return Context;
            return DbScoped.SugarScope.GetConnectionScope(App.MainDbConfigId);
        }

        /// <summary>
        /// 当前是否为主租户（可以直读主库，不需要合并）
        /// </summary>
        private bool IsMainTenant()
        {
            if (!App.IsTenantEnabled()) return true;
            return string.Equals(App.GetCurrentTenantId(), App.MainDbConfigId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 是否需要租户扩展合并（多租户 + 非主租户）
        /// </summary>
        private bool NeedTenantMerge()
        {
            return App.IsTenantEnabled() && !IsMainTenant();
        }

        #endregion

        #region 读取（兼容多租户合并）

        /// <summary>
        /// 查询字典数据列表（管理后台分页，主租户/非多租户只查主库；普通租户合并租户扩展项）
        /// </summary>
        public PagedInfo<SysDictData> SelectDictDataList(SysDictData dictData, PagerInfo pagerInfo)
        {
            var exp = Expressionable.Create<SysDictData>();
            exp.AndIF(!string.IsNullOrEmpty(dictData.DictLabel), it => it.DictLabel.Contains(dictData.DictLabel));
            exp.AndIF(!string.IsNullOrEmpty(dictData.Status), it => it.Status == dictData.Status);
            exp.AndIF(!string.IsNullOrEmpty(dictData.DictType), it => it.DictType == dictData.DictType);

            var needMerge = NeedTenantMerge();
            if (!needMerge)
            {
                return GetPages(exp.ToExpression(), pagerInfo);
            }

            // 普通租户：先查主库全量，再与租户扩展合并，最后内存分页
            var mainList = GetList(exp.ToExpression());
            var extensionList = TenantDb().Queryable<SysTenantDictData>()
                .WhereIF(!string.IsNullOrEmpty(dictData.DictType), x => x.DictType == dictData.DictType)
                .ToList();

            // 管理后台列表不过滤禁用项，由查询 Status 条件自行决定
            var merged = Merge(mainList, extensionList);

            // 合并后按查询条件再次过滤（扩展项可能覆盖了主库项的状态和标签）
            if (!string.IsNullOrEmpty(dictData.Status))
            {
                merged = merged.Where(x => x.Status == dictData.Status).ToList();
            }
            if (!string.IsNullOrEmpty(dictData.DictLabel))
            {
                merged = merged.Where(x => x.DictLabel.Contains(dictData.DictLabel)).ToList();
            }

            var pageNum = pagerInfo.PageNum > 0 ? pagerInfo.PageNum : 1;
            var pageSize = pagerInfo.PageSize > 0 ? pagerInfo.PageSize : 20;
            var paged = merged.Skip((pageNum - 1) * pageSize).Take(pageSize).ToList();

            return new PagedInfo<SysDictData>
            {
                PageIndex = pageNum,
                PageSize = pageSize,
                TotalNum = merged.Count,
                Result = paged,
            };
        }

        /// <summary>
        /// 根据字典类型查询字典数据（业务端使用，自动合并租户扩展）
        /// </summary>
        /// <param name="dictType">字典类型</param>
        /// <returns>合并后的字典数据列表</returns>
        public List<SysDictData> SelectDictDataByType(string dictType)
        {
            string cacheKey = TCacheKey($"SelectDictDataByType_{dictType}");

            if (CacheHelper.GetCache(cacheKey) is List<SysDictData> cached)
            {
                return cached;
            }

            var list = Queryable()
                .Where(f => f.Status == "0" && f.DictType == dictType)
                .OrderBy(it => it.DictSort)
                .ToList();

            if (NeedTenantMerge())
            {
                var extensionList = TenantDb().Queryable<SysTenantDictData>()
                    .Where(x => x.DictType == dictType)
                    .ToList();
                list = Merge(list, extensionList).Where(x => x.Status != "1").ToList();
            }

            CacheHelper.SetCache(cacheKey, list, CACHE_EXPIRE_MINUTES);
            return list;
        }

        /// <summary>
        /// 根据多个字典类型批量查询（前端字典下拉框）
        /// </summary>
        public List<SysDictDataDto> SelectDictDataByTypes(string[] dictTypes)
        {
            if (dictTypes == null || dictTypes.Length == 0) return new List<SysDictDataDto>();

            var sortedTypes = dictTypes.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            string cacheKey = TCacheKey($"SelectDictDataByTypes_{string.Join(",", sortedTypes)}");

            if (CacheHelper.GetCache(cacheKey) is List<SysDictDataDto> cached)
            {
                return cached;
            }

            var mainList = Queryable()
                .Where(f => f.Status == "0" && dictTypes.Contains(f.DictType))
                .OrderBy(it => it.DictSort)
                .Select(it => new SysDictDataDto
                {
                    Label = it.DictLabel,
                    Value = it.DictValue,
                    DictType = it.DictType,
                    CssClass = it.CssClass,
                    ListClass = it.ListClass,
                }, true)
                .ToList();

            if (NeedTenantMerge())
            {
                mainList = MergeTenantDictDataForTypes(sortedTypes.ToList(), mainList);
            }

            CacheHelper.SetCache(cacheKey, mainList, CACHE_EXPIRE_MINUTES);
            return mainList;
        }

        /// <summary>
        /// 根据字典数据ID查询信息
        /// 支持负数 dictCode（表示租户自定义项，绝对值即扩展表 Id）
        /// </summary>
        public SysDictData SelectDictDataById(long dictCode)
        {
            if (dictCode < 0 && NeedTenantMerge())
            {
                var ext = TenantDb().Queryable<SysTenantDictData>()
                    .First(x => x.Id == -dictCode);
                if (ext == null) return null;
                return new SysDictData
                {
                    DictCode = dictCode,
                    DictType = ext.DictType,
                    DictValue = ext.DictValue,
                    DictLabel = ext.DictLabel,
                    DictSort = ext.DictSort,
                    Status = ext.Status,
                    IsDefault = ext.IsDefault,
                    CssClass = ext.CssClass,
                    ListClass = ext.ListClass,
                    Remark = ext.Remark,
                    Create_by = ext.Create_by,
                    Create_time = ext.Create_time,
                    Update_by = ext.Update_by,
                    Update_time = ext.Update_time,
                };
            }

            string CK = TCacheKey($"SelectDictDataByCode_{dictCode}");
            if (CacheHelper.GetCache(CK) is SysDictData cached)
            {
                return cached;
            }

            var mainItem = GetFirst(f => f.DictCode == dictCode);
            if (mainItem != null && NeedTenantMerge())
            {
                var ext = TenantDb().Queryable<SysTenantDictData>()
                    .First(x => x.DictType == mainItem.DictType && x.DictValue == mainItem.DictValue);
                mainItem = ApplySingleOverride(mainItem, ext);
            }

            CacheHelper.SetCache(CK, mainItem, 5);
            return mainItem;
        }

        /// <summary>
        /// 根据字典类型查询自定义sql（仅主库）
        /// </summary>
        public List<SysDictDataDto> SelectDictDataByCustomSql(SysDictType sysDictType)
        {
            return Context.Ado.SqlQuery<SysDictDataDto>(sysDictType?.CustomSql).ToList();
        }

        #endregion

        #region 租户扩展合并

        /// <summary>
        /// 批量类型合并（用于前端下拉框 DictTypes 接口）
        /// 通过临时映射复用通用 Merge 逻辑，避免重复实现覆盖/禁用/新增规则。
        /// </summary>
        private List<SysDictDataDto> MergeTenantDictDataForTypes(List<string> dictTypes, List<SysDictDataDto> mainList)
        {
            var extensionList = TenantDb().Queryable<SysTenantDictData>()
                .Where(x => dictTypes.Contains(x.DictType))
                .ToList();

            // 用临时 SysDictData 承载原始顺序，复用 Merge 后映射回 Dto
            var orderMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var tempMainList = new List<SysDictData>(mainList.Count);
            for (int i = 0; i < mainList.Count; i++)
            {
                var dto = mainList[i];
                var key = GetDictKey(dto.DictType, dto.Value);
                orderMap[key] = i;
                tempMainList.Add(new SysDictData
                {
                    DictType = dto.DictType,
                    DictValue = dto.Value,
                    DictLabel = dto.Label,
                    CssClass = dto.CssClass,
                    ListClass = dto.ListClass,
                    DictSort = i,
                });
            }

            var merged = Merge(tempMainList, extensionList).Where(m => m.Status != "1");
            return merged.Select(m => new SysDictDataDto
            {
                Label = m.DictLabel,
                Value = m.DictValue,
                DictType = m.DictType,
                CssClass = m.CssClass,
                ListClass = m.ListClass,
            }).OrderBy(x => orderMap.GetValueOrDefault(GetDictKey(x.DictType, x.Value), int.MaxValue)).ToList();
        }

        /// <summary>
        /// 合并核心逻辑：主库列表 + 租户扩展 → 完整列表（含被禁用项，状态以扩展表为准）。
        /// 业务端查询在调用后自行 Where(Status != "1") 过滤；管理后台列表由查询条件决定。
        /// </summary>
        private List<SysDictData> Merge(List<SysDictData> mainList, List<SysTenantDictData> extensions)
        {
            var result = new List<SysDictData>();
            var overrideMap = new Dictionary<string, SysTenantDictData>(StringComparer.OrdinalIgnoreCase);
            var extraItems = new List<SysTenantDictData>();

            foreach (var ext in extensions)
            {
                var key = GetDictKey(ext.DictType, ext.DictValue);
                if (ext.OriginalDictCode.HasValue)
                {
                    // 覆盖主库已有项
                    overrideMap[key] = ext;
                }
                else
                {
                    // 租户新增的自定义项（含 Status="1" 的自定义禁用项）
                    extraItems.Add(ext);
                }
            }

            // 处理主库项
            foreach (var item in mainList)
            {
                var key = GetDictKey(item.DictType, item.DictValue);
                if (overrideMap.TryGetValue(key, out var overridden))
                {
                    // 用租户扩展覆盖（状态以扩展表为准，含被禁用项）
                    result.Add(ApplySingleOverride(item, overridden));
                }
                else
                {
                    result.Add(item);
                }
            }

            // 追加租户新增的自定义项
            foreach (var ext in extraItems.OrderBy(x => x.DictSort))
            {
                result.Add(new SysDictData
                {
                    DictCode = -ext.Id, // 负数标识自定义项，绝对值即扩展表 Id
                    DictSort = ext.DictSort,
                    DictLabel = ext.DictLabel,
                    DictValue = ext.DictValue,
                    DictType = ext.DictType,
                    CssClass = ext.CssClass,
                    ListClass = ext.ListClass,
                    IsDefault = ext.IsDefault ?? "N",
                    Status = ext.Status ?? "0",
                });
            }

            return result;
        }

        /// <summary>
        /// 将租户扩展覆盖应用到单条主库数据（保留主库字段，状态以扩展表为准）。
        /// </summary>
        private SysDictData ApplySingleOverride(SysDictData mainItem, SysTenantDictData ext)
        {
            if (ext == null) return mainItem;

            return new SysDictData
            {
                DictCode = mainItem.DictCode,
                DictType = mainItem.DictType,
                DictValue = mainItem.DictValue,
                DictSort = ext.DictSort,
                DictLabel = ext.DictLabel ?? mainItem.DictLabel,
                CssClass = ext.CssClass ?? mainItem.CssClass,
                ListClass = ext.ListClass ?? mainItem.ListClass,
                IsDefault = ext.IsDefault ?? mainItem.IsDefault,
                Status = ext.Status ?? mainItem.Status,
                Remark = ext.Remark ?? mainItem.Remark,
                Create_by = mainItem.Create_by,
                Create_time = mainItem.Create_time,
                Update_by = ext.Update_by,
                Update_time = ext.Update_time,
            };
        }

        /// <summary>
        /// 清除指定字典类型的合并缓存
        /// </summary>
        private void ClearDictTypeCache(string dictType)
        {
            CacheHelper.Remove(TCacheKey($"SelectDictDataByType_{dictType}"));
            // 批量缓存 key 无法精准定位，使用前缀移除
            RemoveCacheByPrefix(TCacheKey("SelectDictDataByTypes_"));
        }

        /// <summary>
        /// 清除单条字典数据缓存
        /// </summary>
        private void ClearDictCodeCache(long dictCode)
        {
            CacheHelper.Remove(TCacheKey($"SelectDictDataByCode_{dictCode}"));
        }

        /// <summary>
        /// 按前缀移除缓存（CacheHelper 内部维护的 _keys 列表）
        /// </summary>
        private void RemoveCacheByPrefix(string prefix)
        {
            foreach (var key in CacheHelper.GetCacheKeys().Where(k => k != null && k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                CacheHelper.Remove(key);
            }
        }

        #endregion

        #region 写入（按租户自动路由）

        /// <summary>
        /// 插入字典数据
        /// 主租户 → 主库 SysDictData
        /// 普通租户 → 租户库 SysTenantDictData（OriginalDictCode 为空表示新增）
        /// </summary>
        public long InsertDictData(SysDictData dict)
        {
            if (NeedTenantMerge())
            {
                var tdb = TenantDb();
                var entity = ToTenantDictData(dict, null);
                var id = tdb.Insertable(entity).ExecuteReturnBigIdentity();
                ClearDictTypeCache(dict.DictType);
                return id;
            }

            var result = Insertable(dict).ExecuteReturnBigIdentity();
            ClearDictTypeCache(dict.DictType);
            return result;
        }

        /// <summary>
        /// 修改字典数据
        /// 主租户 → 更新主库 SysDictData
        /// 普通租户 → 更新或插入租户库 SysTenantDictData
        /// </summary>
        public long UpdateDictData(SysDictData dict)
        {
            if (NeedTenantMerge())
            {
                var tdb = TenantDb();
                SysTenantDictData existing = null;

                if (dict.DictCode < 0)
                {
                    // 自定义项直接按扩展表 Id 定位
                    existing = tdb.Queryable<SysTenantDictData>()
                        .First(x => x.Id == -dict.DictCode);
                }
                else
                {
                    existing = tdb.Queryable<SysTenantDictData>()
                        .First(x => x.DictType == dict.DictType && x.DictValue == dict.DictValue);
                }

                if (existing != null)
                {
                    ApplyToTenantDictData(existing, dict);
                    existing.Update_time = DateTime.Now;
                    tdb.Updateable(existing).ExecuteCommand();
                }
                else
                {
                    // 租户首次修改主库项，写入覆盖记录
                    var mainItem = MainDb().Queryable<SysDictData>()
                        .First(x => x.DictType == dict.DictType && x.DictValue == dict.DictValue);
                    tdb.Insertable(ToTenantDictData(dict, mainItem?.DictCode)).ExecuteCommand();
                }

                ClearDictTypeCache(dict.DictType);
                ClearDictCodeCache(dict.DictCode);
                return 1;
            }

            var result = Update(w => w.DictCode == dict.DictCode, it => new SysDictData()
            {
                Remark = dict.Remark,
                Update_time = DateTime.Now,
                Update_by = dict.Update_by,
                DictSort = dict.DictSort,
                DictLabel = dict.DictLabel,
                DictValue = dict.DictValue,
                Status = dict.Status,
                CssClass = dict.CssClass,
                ListClass = dict.ListClass,
                LangKey = dict.LangKey,
                Extend1 = dict.Extend1,
                Extend2 = dict.Extend2,
            });

            ClearDictCodeCache(dict.DictCode);
            ClearDictTypeCache(dict.DictType);
            return result;
        }

        /// <summary>
        /// 更改状态
        /// </summary>
        public int UpdateStatus(SysDictData data)
        {
            if (NeedTenantMerge())
            {
                var tdb = TenantDb();

                // 负数 dictCode 表示租户自定义项，直接按扩展表 Id 定位
                if (data.DictCode < 0)
                {
                    var ext = tdb.Queryable<SysTenantDictData>()
                        .First(x => x.Id == -data.DictCode);
                    if (ext != null)
                    {
                        ext.Status = data.Status;
                        ext.Update_time = DateTime.Now;
                        ext.Update_by = data.Update_by;
                        tdb.Updateable(ext).UpdateColumns(x => new { x.Status, x.Update_time, x.Update_by }).ExecuteCommand();
                        ClearDictTypeCache(ext.DictType);
                    }
                    return 1;
                }

                // 前端可能只传 DictCode + Status，或漏传 DictLabel/DictType/DictValue，缺失时从主库补全
                if (string.IsNullOrEmpty(data.DictType) || string.IsNullOrEmpty(data.DictValue) || string.IsNullOrEmpty(data.DictLabel))
                {
                    if (data.DictCode > 0)
                    {
                        var mainItem = MainDb().Queryable<SysDictData>()
                            .First(x => x.DictCode == data.DictCode);
                        if (mainItem != null)
                        {
                            data.DictType = mainItem.DictType;
                            data.DictValue = mainItem.DictValue;
                            data.DictLabel = mainItem.DictLabel;
                            data.DictSort = mainItem.DictSort;
                            data.IsDefault = mainItem.IsDefault;
                            data.CssClass = mainItem.CssClass;
                            data.ListClass = mainItem.ListClass;
                            data.Remark = mainItem.Remark;
                        }
                    }
                    else
                    {
                        // 租户自定义项没有 DictCode，前端应传 DictType+DictValue
                        throw new CustomException("字典类型和字典值不能为空，请确认前端是否完整传参");
                    }
                }

                var existing = tdb.Queryable<SysTenantDictData>()
                    .First(x => x.DictType == data.DictType && x.DictValue == data.DictValue);

                if (existing != null)
                {
                    existing.Status = data.Status;
                    existing.Update_time = DateTime.Now;
                    existing.Update_by = data.Update_by;
                    tdb.Updateable(existing).UpdateColumns(x => new { x.Status, x.Update_time, x.Update_by }).ExecuteCommand();
                }
                else
                {
                    // 对主库项首次进行状态变更，写入覆盖记录
                    var mainItem = data.DictCode > 0
                        ? MainDb().Queryable<SysDictData>().First(x => x.DictCode == data.DictCode)
                        : null;

                    tdb.Insertable(new SysTenantDictData
                    {
                        DictType = data.DictType,
                        DictValue = data.DictValue,
                        DictLabel = data.DictLabel,
                        DictSort = data.DictSort,
                        Status = data.Status,
                        IsDefault = data.IsDefault ?? mainItem?.IsDefault ?? "N",
                        CssClass = data.CssClass ?? mainItem?.CssClass,
                        ListClass = data.ListClass ?? mainItem?.ListClass,
                        OriginalDictCode = data.DictCode > 0 ? data.DictCode : null,
                        Create_by = data.Update_by,
                        Create_time = DateTime.Now,
                        Remark = data.Remark ?? mainItem?.Remark,
                    }).ExecuteCommand();
                }

                ClearDictTypeCache(data.DictType);
                ClearDictCodeCache(data.DictCode);
                return 1;
            }

            return Update(data, it => new { it.Status }, f => f.DictCode == data.DictCode);
        }

        /// <summary>
        /// 批量删除字典数据
        /// 主租户 → 删除主库 SysDictData
        /// 普通租户 → 对主库项标记禁用（Status=1）；对租户自定义项直接删除
        /// </summary>
        public int DeleteDictDataByIds(long[] dictCodes)
        {
            if (dictCodes == null || dictCodes.Length == 0) return 0;

            if (NeedTenantMerge())
            {
                var tdb = TenantDb();
                var mainDb = MainDb();

                // 分离主库项（正数）和自定义项（负数，绝对值即扩展表 Id）
                var mainCodes = dictCodes.Where(x => x > 0).ToList();
                var customIds = dictCodes.Where(x => x < 0).Select(x => -x).ToList();

                // 1. 处理主库项：标记禁用
                if (mainCodes.Count > 0)
                {
                    var mainItems = mainDb.Queryable<SysDictData>()
                        .Where(x => mainCodes.Contains(x.DictCode))
                        .Select(x => new { x.DictCode, x.DictType, x.DictValue, x.DictLabel, x.DictSort })
                        .ToList();

                    var extensionItems = tdb.Queryable<SysTenantDictData>()
                        .Where(x => mainCodes.Contains(x.OriginalDictCode ?? 0))
                        .ToList();

                    var extensionByKey = extensionItems.ToDictionary(
                        x => GetDictKey(x.DictType, x.DictValue),
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var item in mainItems)
                    {
                        var key = GetDictKey(item.DictType, item.DictValue);
                        if (extensionByKey.TryGetValue(key, out var existing))
                        {
                            existing.Status = "1";
                            existing.Update_time = DateTime.Now;
                            tdb.Updateable(existing).UpdateColumns(x => new { x.Status, x.Update_time }).ExecuteCommand();
                        }
                        else
                        {
                            tdb.Insertable(new SysTenantDictData
                            {
                                DictType = item.DictType,
                                DictValue = item.DictValue,
                                DictLabel = item.DictLabel,
                                DictSort = item.DictSort,
                                Status = "1",
                                OriginalDictCode = item.DictCode,
                                Create_time = DateTime.Now,
                            }).ExecuteCommand();
                        }
                        ClearDictTypeCache(item.DictType);
                        ClearDictCodeCache(item.DictCode);
                    }
                }

                // 2. 处理自定义项：物理删除
                if (customIds.Count > 0)
                {
                    var customItems = tdb.Queryable<SysTenantDictData>()
                        .Where(x => customIds.Contains(x.Id))
                        .ToList();
                    tdb.Deleteable<SysTenantDictData>().In(customIds).ExecuteCommand();
                    foreach (var ext in customItems)
                    {
                        ClearDictTypeCache(ext.DictType);
                    }
                }

                return dictCodes.Length;
            }

            return Delete(dictCodes);
        }

        /// <summary>
        /// 同步修改字典类型（主租户/非多租户模式：只更新主库；普通租户模式：同时同步租户扩展表）
        /// </summary>
        public int UpdateDictDataType(string old_dictType, string new_dictType)
        {
            int rows = Context.Updateable<SysDictData>()
                .SetColumns(t => new SysDictData() { DictType = new_dictType })
                .Where(f => f.DictType == old_dictType)
                .ExecuteCommand();

            if (NeedTenantMerge())
            {
                rows += TenantDb().Updateable<SysTenantDictData>()
                    .SetColumns(t => new SysTenantDictData() { DictType = new_dictType })
                    .Where(f => f.DictType == old_dictType)
                    .ExecuteCommand();
            }

            ClearDictTypeCache(old_dictType);
            ClearDictTypeCache(new_dictType);
            return rows;
        }

        #endregion

        #region 实体映射辅助方法

        /// <summary>
        /// 将主库 SysDictData 转换为租户扩展表实体
        /// </summary>
        /// <param name="source">主库字典数据</param>
        /// <param name="originalDictCode">被覆盖的主库 DictCode，null 表示租户自定义新增项</param>
        private SysTenantDictData ToTenantDictData(SysDictData source, long? originalDictCode)
        {
            return new SysTenantDictData
            {
                DictType = source.DictType,
                DictValue = source.DictValue,
                DictLabel = source.DictLabel,
                DictSort = source.DictSort,
                Status = source.Status ?? "0",
                IsDefault = source.IsDefault ?? "N",
                CssClass = source.CssClass,
                ListClass = source.ListClass,
                OriginalDictCode = originalDictCode,
                Create_by = source.Create_by,
                Create_time = source.Create_time,
                Remark = source.Remark,
            };
        }

        /// <summary>
        /// 用主库 SysDictData 更新租户扩展表实体（保留 OriginalDictCode、Create 等字段）
        /// </summary>
        private void ApplyToTenantDictData(SysTenantDictData target, SysDictData source)
        {
            target.DictLabel = source.DictLabel;
            target.DictSort = source.DictSort;
            target.Status = source.Status ?? "0";
            target.IsDefault = source.IsDefault ?? "N";
            target.CssClass = source.CssClass;
            target.ListClass = source.ListClass;
            target.Update_by = source.Update_by;
            target.Remark = source.Remark;
        }

        #endregion
    }
}
