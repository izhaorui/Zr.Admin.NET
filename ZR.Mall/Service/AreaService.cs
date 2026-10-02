using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;
using ZR.Model.Enum;
using ZR.Model.Public;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 行政区划服务：地区树查询 + 收货地区编码解析
    /// </summary>
    /// <remarks>
    /// 本服务是运费模板「按地区排除」与「包邮」的数据底座，同时承担一件更重要的事：
    /// **把不标准的地区写法收敛成字典里的标准编码**。收货地址在运费改造前只存名称，
    /// 存量数据还夹着「市辖区」这类占位名、已撤销区县与连写形式，因此下单链路必须能兜底反查。
    /// </remarks>
    [AppService(ServiceType = typeof(IAreaService))]
    public class AreaService : BaseService<Area>, IAreaService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 地区表是否可用。
        /// </summary>
        /// <remarks>
        /// <c>sys_area</c> 是**主库**表：结构随启动迁移自动创建，但**数据只在 <c>--initdb</c> 时灌**，
        /// 故首次部署后、运维还没跑 initdb 时这张表可能为空甚至不存在。
        /// 那时若直接查库会抛 SqlSugar 异常并**打崩下单**（地区解析在运费计算的关键路径上）。
        /// 故所有查询入口都先探表，缺失时按"地区不可判定"降级：
        /// 运费按正常计费、只是不做地区排除与包邮判断，交易本身不受影响。
        /// </remarks>
        private static bool _tableReady;

        /// <summary>
        /// 探一次表是否可用；结果缓存，避免每次下单都去查系统表。
        /// </summary>
        private bool EnsureTable()
        {
            if (_tableReady) return true;
            try
            {
                _tableReady = Context.DbMaintenance.IsAnyTable(Context.EntityMaintenance.GetEntityInfo<Area>().DbTableName, false);
            }
            catch (Exception ex)
            {
                // 连系统表元数据都取不到（如主库连接未配置）：一律按不可用处理，只记一次 Warn
                _tableReady = false;
                logger.Warn(ex, "[地区字典] 探测 sys_area 失败，运费将不做地区匹配（不影响下单）");
            }
            if (!_tableReady)
            {
                logger.Warn("[地区字典] 主库 sys_area 不可用，运费将不做地区匹配（不影响下单）。请执行 --initdb 灌入地区种子");
            }
            return _tableReady;
        }

        /// <summary>
        /// 按父级 Id 取直接下级，供后台级联下拉懒加载。
        /// </summary>
        /// <param name="parentId">父级 Id；传 0 取省级</param>
        public List<AreaTreeDto> GetChildren(long parentId)
        {
            if (!EnsureTable())
            {
                return new List<AreaTreeDto>();
            }

            var children = Queryable()
                .Where(x => x.ParentId == parentId)
                .OrderBy(x => x.SortId)
                .ToList();

            if (children.Count == 0)
            {
                return new List<AreaTreeDto>();
            }

            // 一次查出任一子节点是否还有下级，避免逐节点查库（N+1）。
            // 区县是叶子（Level=3），必然无下级，可直接判定，不必查库。
            var cityIds = children
                .Where(x => x.Level == (int)AreaLevelEnum.City)
                .Select(x => x.Id)
                .ToList();
            var expandable = cityIds.Count == 0
                ? new HashSet<long>()
                : Queryable()
                    .Where(x => cityIds.Contains(x.ParentId))
                    .Select(x => x.ParentId)
                    .ToList()
                    .ToHashSet();

            return children.Select(x => new AreaTreeDto
            {
                Id = x.Id,
                AreaCode = x.AreaCode,
                Name = x.Name,
                ParentId = x.ParentId,
                Level = x.Level,
                HasChildren = x.Level < (int)AreaLevelEnum.District || expandable.Contains(x.Id)
            }).ToList();
        }

        /// <summary>
        /// 按编码取地区节点；未命中返回 null。
        /// </summary>
        public Area GetByCode(string areaCode)
        {
            if (string.IsNullOrWhiteSpace(areaCode) || !EnsureTable()) return null;
            return Queryable().First(x => x.AreaCode == areaCode.Trim());
        }

        /// <summary>
        /// 批量按编码取地区节点，返回「编码 → 节点」字典；入参为空时返回空字典。
        /// </summary>
        /// <remarks>运费按编码批量匹配排除地区时用，避免逐个编码查库</remarks>
        public Dictionary<string, Area> GetByCodes(IEnumerable<string> areaCodes)
        {
            var codes = areaCodes?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct()
                .ToList() ?? new List<string>();

            var result = new Dictionary<string, Area>(StringComparer.Ordinal);
            if (codes.Count == 0 || !EnsureTable()) return result;

            foreach (var a in Queryable().Where(x => codes.Contains(x.AreaCode)).ToList())
            {
                result[a.AreaCode] = a;
            }
            return result;
        }

        /// <summary>
        /// 解析收货地区：<b>编码权威、名称兜底</b>，并沿父链向上回溯纠正上级。
        /// </summary>
        /// <remarks>
        /// 三条规则：①编码权威、名称只是提示（可自愈填错的省名）；②更深层级优先（区 → 市 → 省）；
        /// ③名称反查仅在编码缺失时用、且按父级限定（全国多「城关区」重名）。
        /// 任何一级查不到都只留空、不报错，由上层按"地区不可判定"降级。
        /// 详见 document/商城运费模板设计.md 第五节。
        /// </remarks>
        /// <param name="query">名称 + 编码（编码可空）</param>
        /// <returns>解析结果；未命中的级别编码为 null、Id 为 0，名称沿用入参</returns>
        public AreaCodesDto ResolveCodes(AreaQueryDto query)
        {
            var result = new AreaCodesDto();
            // 表不可用时直接返回空结果：调用方据此认为"地区不可判定"，
            // 运费按正常计费而不是报错 —— 绝不能因为一张字典表缺失就拦住成交
            if (query == null || !EnsureTable())
            {
                return result;
            }

            // ---- 第一轮：编码定位，从最深级往上，命中即沿父链带回全部上级 ----
            var district = ByCode((int)AreaLevelEnum.District, query.DistrictCode);
            var city = district == null ? ByCode((int)AreaLevelEnum.City, query.CityCode) : null;
            var province = district == null && city == null
                ? ByCode((int)AreaLevelEnum.Province, query.ProvinceCode)
                : null;

            if (district != null)
            {
                // 区级编码可信度最高：市与省都从它的父链取，忽略入参里可能矛盾的市码/省码。
                // city 仍取不到（父编码在字典中缺失，属数据异常）时，省级留空由第二轮按名兜底。
                city = ByCode((int)AreaLevelEnum.City, district.ParentCode);
                if (city != null)
                {
                    province = ByCode((int)AreaLevelEnum.Province, city.ParentCode);
                }
            }
            else if (city != null)
            {
                // 已有市码：省从市码的父级取（用户填错省名时以此自愈）；取不到才回落到入参省码
                province = ByCode((int)AreaLevelEnum.Province, city.ParentCode) ?? province;
            }

            // ---- 第二轮：仍缺失的级别按名称反查，父级用已解析出的上级限定 ----
            if (province == null)
            {
                province = ByName((int)AreaLevelEnum.Province, null, query.Province);
            }
            if (city == null && province != null)
            {
                city = ByName((int)AreaLevelEnum.City, province.AreaCode, query.City);
            }
            if (district == null && city != null)
            {
                district = ByName((int)AreaLevelEnum.District, city.AreaCode, query.District);
            }

            if (province != null)
            {
                result.ProvinceCode = province.AreaCode;
                result.ProvinceId = province.Id;
                result.ProvinceName = province.Name;
            }
            if (city != null)
            {
                result.CityCode = city.AreaCode;
                result.CityId = city.Id;
                result.CityName = city.Name;
            }
            if (district != null)
            {
                result.DistrictCode = district.AreaCode;
                result.DistrictId = district.Id;
                result.DistrictName = district.Name;
            }

            LogUnresolved(query, result);
            return result;
        }

        /// <summary>
        /// 按编码取指定层级的节点。层级不符（如把省码当市码）视为未命中，交由名称反查兜底。
        /// </summary>
        private Area ByCode(int level, string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            var area = Queryable().First(x => x.AreaCode == code.Trim());
            return area != null && area.Level == level ? area : null;
        }

        /// <summary>
        /// 按「层级 + 父级编码 + 名称」反查节点；<paramref name="parentCode"/> 为 null 表示顶级。
        /// </summary>
        private Area ByName(int level, string parentCode, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var target = name.Trim();
            if (parentCode == null)
            {
                return Queryable().First(x => x.Level == level && x.Name == target
                    && (x.ParentCode == null || x.ParentCode == string.Empty));
            }
            return Queryable().First(x => x.Level == level && x.Name == target && x.ParentCode == parentCode);
        }

        /// <summary>
        /// 记录解析不完整的档位。
        /// </summary>
        /// <remarks>
        /// 只记 Debug：地区解析不到是"降级计运费"的正常路径（历史脏数据、管理员还没配运费模板都会走到），
        /// 记 Info 会把正常业务刷成噪音。真正需要人工介入的是"配了不配送地区却匹配不上"，
        /// 那由运费编排服务在命中失败时单独记 Warn。
        /// </remarks>
        private void LogUnresolved(AreaQueryDto query, AreaCodesDto result)
        {
            if (result.ProvinceCode != null && result.CityCode != null && result.DistrictCode != null)
            {
                return;
            }
            logger.Debug($"[地区解析] 部分层级未命中字典，运费将按降级处理：输入 省={query.Province}/{query.ProvinceCode} 市={query.City}/{query.CityCode} 区={query.District}/{query.DistrictCode} → 解析 省={result.ProvinceCode} 市={result.CityCode} 区={result.DistrictCode}");
        }
    }
}
