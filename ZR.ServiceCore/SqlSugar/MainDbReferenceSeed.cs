using Infrastructure;
using SqlSugar.IOC;
using ZR.Model.Enum;
using ZR.Model.Public;

namespace ZR.ServiceCore.SqlSugar
{
    /// <summary>
    /// 主库公共参考数据的幂等灌数（当前为行政区划 <c>sys_area</c>）。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="DbMigrationService"/> 同层：结构迁移与参考数据灌入同属"库初始化"职责。
    /// 由 <see cref="InitTable.InitDb"/> 无条件调用，**不受 <c>ModuleInit.*</c> 开关控制** ——
    /// 否则模块关闭时表被无条件建出却永不灌数据，留下一张比缺表更隐蔽的空表。
    /// </remarks>
    public static class MainDbReferenceSeed
    {
        /// <summary>内嵌地区种子资源名（须与 csproj 中 EmbeddedResource 的 LogicalName 一致）</summary>
        private const string AreaSeedResourceName = "ZR.ServiceCore.SqlSeed.area-seed.json";

        /// <summary>单批插入行数：一次太多会撑大 SQL 语句与参数表，按 500 一批折中</summary>
        private const int AreaSeedBatchSize = 500;

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 幂等灌入行政区划种子数据（<c>sys_area</c>）。
        /// </summary>
        /// <remarks>
        /// 失败只记 Warn、不抛异常：运费是增量能力，为它让整个 <c>--initdb</c> 失败会连带中断其他模块初始化。
        /// </remarks>
        /// <param name="db">主库连接；传 null 时按 <c>App.MainDbConfigId</c> 自行解析</param>
        /// <returns>实际执行了灌数返回 true；跳过（已有数据 / 无种子）返回 false</returns>
        public static bool EnsureAreas(ISqlSugarClient db = null)
        {
            try
            {
                db ??= DbScoped.SugarScope.GetConnectionScope(App.MainDbConfigId);

                // 表不存在则建表、已存在则补列（幂等）。不只依赖 Migrate：
                // 本方法也会被单独调用（如后台结构同步），不该要求调用方先跑一遍全量迁移。
                DbMigrationService.EnsureEntitySchemas(db, new[] { typeof(Area) });

                // 幂等：表非空即视为已灌过。字典表无增删改接口，不存在"需要补齐"的中间态。
                if (db.Queryable<Area>().Any())
                {
                    logger.Debug("[主库种子] 行政区划已有数据，跳过灌入");
                    return false;
                }

                var seeds = LoadAreaSeeds();
                if (seeds.Count == 0)
                {
                    logger.Warn("[主库种子] 地区种子为空或资源缺失，运费将不做地区匹配（不影响下单）");
                    return false;
                }

                // 分三轮插入：每轮插入后回查本级 Id，供下一轮填 ParentId。
                // 种子 JSON 已按 省 → 市 → 区 排序，故某轮的父级必然已在库中。
                var idByCode = new Dictionary<string, long>(StringComparer.Ordinal);
                InsertLevel(db, seeds, AreaLevelEnum.Province, idByCode);
                InsertLevel(db, seeds, AreaLevelEnum.City, idByCode);
                InsertLevel(db, seeds, AreaLevelEnum.District, idByCode);

                logger.Info($"[主库种子] 行政区划灌入完成：共 {seeds.Count} 条（省 {seeds.Count(x => x.Level == (int)AreaLevelEnum.Province)} / 市 {seeds.Count(x => x.Level == (int)AreaLevelEnum.City)} / 区 {seeds.Count(x => x.Level == (int)AreaLevelEnum.District)}）");
                return true;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "[主库种子] 行政区划灌入失败，运费将不做地区匹配（不影响下单）");
                return false;
            }
        }

        /// <summary>
        /// 插入某一级地区，并把该级「编码 → Id」并入 <paramref name="idByCode"/> 供下级填父级。
        /// </summary>
        private static void InsertLevel(ISqlSugarClient db, List<AreaSeedRow> seeds,
            AreaLevelEnum level, Dictionary<string, long> idByCode)
        {
            var levelRows = seeds
                .Where(x => x.Level == (int)level)
                .Select(x => new Area
                {
                    AreaCode = x.AreaCode,
                    Name = x.Name,
                    ParentCode = x.ParentCode,
                    // 省级与无父级行取 0；其余父级编码此时必然已在 idByCode 中（种子按层级有序）
                    ParentId = level == AreaLevelEnum.Province || x.ParentCode == null
                        ? 0
                        : idByCode.TryGetValue(x.ParentCode, out var pid) ? pid : 0,
                    Level = x.Level,
                    SortId = x.SortId
                })
                .ToList();

            if (levelRows.Count == 0)
            {
                return;
            }

            for (var i = 0; i < levelRows.Count; i += AreaSeedBatchSize)
            {
                db.Insertable(levelRows.Skip(i).Take(AreaSeedBatchSize).ToList()).ExecuteCommand();
            }

            foreach (var a in db.Queryable<Area>()
                .Where(x => x.Level == (int)level)
                .Select(x => new { x.AreaCode, x.Id })
                .ToList())
            {
                idByCode[a.AreaCode] = a.Id;
            }
        }

        /// <summary>
        /// 读取内嵌地区种子。资源缺失或内容损坏都返回空列表（调用方只记 Warn，不抛异常）。
        /// </summary>
        private static List<AreaSeedRow> LoadAreaSeeds()
        {
            using var stream = typeof(MainDbReferenceSeed).Assembly
                .GetManifestResourceStream(AreaSeedResourceName);
            if (stream == null)
            {
                logger.Warn($"[主库种子] 未找到内嵌地区种子资源：{AreaSeedResourceName}（请检查 ZR.ServiceCore.csproj 的 EmbeddedResource 配置）");
                return new List<AreaSeedRow>();
            }

            try
            {
                using var reader = new StreamReader(stream);
                // 显式写全限定名：项目全局同时可见 Newtonsoft 与 System.Text.Json，裸写会 CS0104 二义。
                var rows = System.Text.Json.JsonSerializer.Deserialize<List<AreaSeedRow>>(reader.ReadToEnd());
                return rows ?? new List<AreaSeedRow>();
            }
            catch (System.Text.Json.JsonException ex)
            {
                logger.Warn(ex, "[主库种子] 地区种子内容不是合法 JSON，已跳过灌入");
                return new List<AreaSeedRow>();
            }
        }
    }

    /// <summary>
    /// 内嵌地区种子的一行。字段名与 area-seed.json 的键逐字一致（PascalCase），可直接绑定、无需映射表。
    /// </summary>
    internal class AreaSeedRow
    {
        /// <summary>行政编码</summary>
        public string AreaCode { get; set; }

        /// <summary>名称</summary>
        public string Name { get; set; }

        /// <summary>父级编码；省级为 null</summary>
        public string ParentCode { get; set; }

        /// <summary>层级：1 省 / 2 市 / 3 区县</summary>
        public int Level { get; set; }

        /// <summary>排序号</summary>
        public int SortId { get; set; }
    }
}
