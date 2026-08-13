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
        /// 获取字典选择框列表（仅启用状态的字典类型）
        /// </summary>
        /// <returns></returns>
        public List<SysDictType> GetDictTypeOptionSelect()
        {
            return Queryable().Where(f => f.Status == "0").ToList();
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
        /// 自定义字典 SQL 白名单关键字（小写），命中任一即禁止执行，防止注入。
        /// 仅允许只读 SELECT，禁止写操作/DDL/多语句/系统存储过程。
        /// </summary>
        private static readonly string[] ForbiddenSqlKeywords =
        {
            ";", "--", "/*", "*/", "drop", "delete", "update", "insert", "truncate",
            "alter", "create", "exec", "execute", "grant", "revoke", "xp_", "sp_",
            "union", "into", "merge", "begin", "declare", "waitfor", "shutdown"
        };

        /// <summary>
        /// 校验自定义字典 SQL 是否合法（仅允许只读 SELECT 单语句）。
        /// 返回 false 表示疑似注入/非法，禁止执行。internal 供执行层兜底复用。
        /// </summary>
        internal static bool IsSafeCustomSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return false;
            }
            var normalized = sql.Trim().Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            if (!normalized.StartsWith("select", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            // 去除字符串字面量（'...'）后再检测危险关键字，避免误伤正常列名
#if NET6_0_OR_GREATER
            var stripped = System.Text.RegularExpressions.Regex.Replace(normalized, "'(?:[^']|'')*'", "''", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
#else
            var stripped = normalized;
#endif
            var lower = stripped.ToLowerInvariant();
            foreach (var kw in ForbiddenSqlKeywords)
            {
                var idx = lower.IndexOf(kw, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    // 排除列名/表名中恰好包含关键字子串的情况（如字段名含"describe"）做简单边界判断
                    if (kw.Length == 1) // 仅 ";" 这类符号直接拦截
                    {
                        return false;
                    }
                    // 关键字前后应为非字母数字，避免 "updated_at" 误判 "update"
                    var before = idx > 0 ? lower[idx - 1] : ' ';
                    var after = idx + kw.Length < lower.Length ? lower[idx + kw.Length] : ' ';
                    var isWordBoundary = !char.IsLetterOrDigit(before) && !char.IsLetterOrDigit(after);
                    if (isWordBoundary)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 根据字典类型查询自定义sql（仅允许只读 SELECT 单语句，经白名单校验）
        /// </summary>
        public List<SysDictDataDto> SelectDictDataByCustomSql(string dictType)
        {
            var dictInfo = Queryable()
                .Where(f => f.DictType == dictType).First();
            if (dictInfo == null || !IsSafeCustomSql(dictInfo.CustomSql))
            {
                if (dictInfo != null)
                {
                    Log.WriteLine(ConsoleColor.Yellow, $"[SysDict] 自定义字典 SQL 未通过安全校验，已拒绝执行。DictType={dictInfo.DictType}");
                }
                return new List<SysDictDataDto>();
            }
            return DictDataService.SelectDictDataByCustomSql(dictInfo) ?? new List<SysDictDataDto>();
        }
    }
}
