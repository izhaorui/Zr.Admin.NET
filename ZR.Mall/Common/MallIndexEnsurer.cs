namespace ZR.Mall.Common
{
    /// <summary>
    /// 商城热查询索引的"确保存在"逻辑（幂等、容错）。
    ///
    /// 为什么单独有这一处：SqlSugar 的 <c>[SugarIndex]</c> 只在**建表**时生成索引
    /// （见 DbMigrationService.EnsureEntitySchema —— 对已存在的表只补列、不补索引），
    /// 因此存量库、以及每个存量租户库都拿不到事后新增的索引，只能靠这里显式补。
    ///
    /// 两处共用（与 <see cref="ProductFullTextIndexEnsurer"/> 同一模式）：
    /// - 商城库初始化（<see cref="MallTenantInitializer.InitCore"/>，由 <c>--initdb</c> 与新建租户触发）；
    /// - 后台「商城库同步」（MallDbSyncController.Sync，运营点一下即可）。
    ///
    /// 刻意设计：
    /// - <b>不抛异常</b>：无 DDL 权限、表/列不存在等一律记日志并跳过，不阻断初始化与同步；
    /// - <b>幂等</b>：按索引名判断，已存在直接跳过，可安全反复执行；
    /// - <b>不放在 Web 启动链路</b>：大表建非聚集索引可能耗时，只挂在显式的初始化与同步动作里
    ///   （与全文索引同一考虑，大表建议低峰执行）；
    /// - <b>只做"加"，不做"改/删"</b>：不删除、不重建已有索引，对存量库无破坏性。
    /// </summary>
    public static class MallIndexEnsurer
    {
        // ── 索引名集中在这里，Model 上的 [SugarIndex] 直接引用这些常量 ──
        // 目的：索引名只有一个出处，避免"特性里一个名、补索引时另一个名"导致存量库被重复建索引。

        /// <summary>订单：状态 + 时间 组合索引（超时关单、自动确认收货、订单概览统计）</summary>
        public const string OrderStatusCreate = "idx_oms_order_status_create";

        /// <summary>订单：时间窗口（后台订单列表/导出的默认"最近 7 天"）</summary>
        public const string OrderCreate = "idx_oms_order_create";

        /// <summary>订单项：按订单取明细（订单详情、取消/退款/关单回补库存）</summary>
        public const string OrderItemOrder = "idx_oms_order_item_order";

        /// <summary>订单日志：按订单 Id 取日志</summary>
        public const string OrderLogOrder = "idx_oms_order_log_order";

        /// <summary>订单日志：按订单号取日志（后台日志页的 orderNo 分支）</summary>
        public const string OrderLogNo = "idx_oms_order_log_no";

        /// <summary>SKU：按商品取 SKU（商品详情/列表 Includes、下单校验、SKU 维护回写）</summary>
        public const string SkusProduct = "idx_mms_skus_product";

        /// <summary>商品：C 端在售列表（在售 + 未删除 [+ 分类]）</summary>
        public const string ProductFront = "idx_mms_product_front";

        /// <summary>索引定义（表名/列名一律取实体对应的数据库列名，用词与实体保持一致）</summary>
        private sealed class IndexDef
        {
            public string Table;
            public string Name;
            public string[] Columns;
            public string Reason;
        }

        /// <summary>
        /// 需要确保存在的索引清单。每一条都对应代码里真实存在的查询条件，不做"预防性"加索引。
        /// </summary>
        private static readonly IndexDef[] Indexes =
        {
            new IndexDef
            {
                Table = "oms_order",
                Name = OrderStatusCreate,
                Columns = new[] { "OrderStatus", "IsDelete", "CreateTime" },
                Reason = "Job_ClosePendingOrder（待付款+超时）与 Job_AutoConfirmReceipt（已发货+超时）每轮都按它扫，订单概览统计也走状态前缀"
            },
            new IndexDef
            {
                Table = "oms_order",
                Name = OrderCreate,
                Columns = new[] { "IsDelete", "CreateTime" },
                Reason = "后台订单列表/导出在未选状态时默认只看最近 7 天（OMSOrderService.QueryExp）"
            },
            new IndexDef
            {
                Table = "oms_order_item",
                Name = OrderItemOrder,
                Columns = new[] { "OrderId" },
                Reason = "订单详情 Includes(Items)、取消/退款/关单按订单回补库存，均按 OrderId 过滤；此前无任何索引"
            },
            new IndexDef
            {
                Table = "oms_order_log",
                Name = OrderLogOrder,
                Columns = new[] { "OrderId" },
                Reason = "后台订单日志详情 GetByOrder(OrderId)；此前无任何索引"
            },
            new IndexDef
            {
                Table = "oms_order_log",
                Name = OrderLogNo,
                Columns = new[] { "OrderNo" },
                Reason = "后台订单日志 GetByOrderNo(OrderNo) 分支"
            },
            new IndexDef
            {
                Table = "mms_skus",
                Name = SkusProduct,
                Columns = new[] { "ProductId" },
                Reason = "商品详情/列表 Includes(Skus)、下单取 SKU、SKU 维护按商品回写汇总，均按 ProductId 过滤"
            },
            new IndexDef
            {
                Table = "mms_product",
                Name = ProductFront,
                Columns = new[] { "SaleStatus", "IsDelete", "CategoryId" },
                Reason = "C 端在售列表固定按 在售+未删除 过滤，分类页再叠加 CategoryId"
            },
        };

        /// <summary>
        /// 确保商城热查询索引存在。返回可读的处理结果（供同步接口回显）；任何失败都不抛异常。
        /// </summary>
        public static string Ensure(ISqlSugarClient db)
        {
            if (db == null)
            {
                return "商城库连接为空，跳过索引创建";
            }

            DbType dbType;
            try
            {
                dbType = db.CurrentConnectionConfig.DbType;
            }
            catch (Exception ex)
            {
                Log.WriteLine(ConsoleColor.Yellow, $"[商城索引] 读取数据库类型失败，跳过：{ex.Message}");
                return $"读取数据库类型失败，跳过：{ex.Message}";
            }

            if (dbType != DbType.SqlServer && dbType != DbType.MySql)
            {
                return $"数据库 {dbType} 未适配自动补索引，跳过（索引可用手工 DDL 按 MallIndexEnsurer 的定义创建）";
            }

            var created = 0;
            var skipped = 0;
            var problems = new List<string>();

            foreach (var def in Indexes)
            {
                try
                {
                    // 表不存在（未初始化/未同步）：跳过。不在这里建表 —— 建表是 EnsureEntitySchemas 的职责。
                    if (!db.DbMaintenance.IsAnyTable(def.Table, false))
                    {
                        skipped++;
                        continue;
                    }
                    if (Exists(db, dbType, def))
                    {
                        skipped++;
                        continue;
                    }

                    db.Ado.ExecuteCommand(BuildCreateSql(dbType, def));
                    created++;
                    Log.WriteLine(ConsoleColor.Green,
                        $"[商城索引] 已创建索引 {def.Name}（表={def.Table} 列={string.Join(",", def.Columns)}）：{def.Reason}");
                }
                catch (Exception ex)
                {
                    // 无 DDL 权限、列不存在（老库）等落到这里：只告警，不影响其它索引与整体同步流程
                    problems.Add($"{def.Name}: {ex.Message}");
                    Log.WriteLine(ConsoleColor.Yellow, $"[商城索引] 创建索引 {def.Name}（表={def.Table}）失败：{ex.Message}");
                }
            }

            var summary = $"已创建 {created} 个，已存在/跳过 {skipped} 个";
            if (problems.Count > 0)
            {
                summary += $"；失败 {problems.Count} 个：{string.Join(" | ", problems.Take(3))}";
            }
            return summary;
        }

        /// <summary>
        /// 索引是否已存在。表名/索引名全部来自本类的编译期常量（非用户输入），不构成注入面。
        /// </summary>
        private static bool Exists(ISqlSugarClient db, DbType dbType, IndexDef def)
        {
            if (dbType == DbType.SqlServer)
            {
                return db.Ado.GetInt(
                    $"SELECT COUNT(1) FROM sys.indexes WHERE object_id = OBJECT_ID('{def.Table}') AND name = '{def.Name}'") > 0;
            }

            return db.Ado.GetInt(
                "SELECT COUNT(1) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND INDEX_NAME = @index",
                new { table = def.Table, index = def.Name }) > 0;
        }

        /// <summary>
        /// 生成建索引 DDL。刻意不加 ONLINE 等版本相关选项：各版本/各版本（Standard/Express）支持度不同，
        /// 加了可能在低版本上直接报错，反而让索引建不出来。
        /// </summary>
        private static string BuildCreateSql(DbType dbType, IndexDef def)
        {
            if (dbType == DbType.SqlServer)
            {
                var cols = string.Join(",", def.Columns.Select(c => $"[{c}]"));
                return $"CREATE NONCLUSTERED INDEX [{def.Name}] ON [{def.Table}] ({cols})";
            }

            var myCols = string.Join(",", def.Columns.Select(c => $"`{c}`"));
            return $"CREATE INDEX `{def.Name}` ON `{def.Table}` ({myCols})";
        }
    }
}
