namespace ZR.Mall.Common
{
    /// <summary>
    /// 商品名全文索引的"确保存在"逻辑（幂等、容错）。
    ///
    /// 两处共用：
    /// - 商城库初始化（<see cref="MallTenantInitializer.InitCore"/>，由 <c>--initdb</c> 与新建租户触发）；
    /// - 后台「商城库同步」（MallDbSyncController.Sync，运营点一下即可）。
    ///
    /// 为什么单独成一处：SQL Server / MySQL 建全文索引的语法与就绪探测方式不同，
    /// 且要保证"任何失败都不阻断初始化/同步" —— 搜索本身有 LIKE 兜底，全文索引只是加速。
    ///
    /// 刻意设计：
    /// - <b>不抛异常</b>：未装全文组件、无 DDL 权限、表不存在等一律记日志并跳过，返回可读结果；
    /// - <b>幂等</b>：已存在直接跳过，可安全地每次初始化/同步都跑；
    /// - <b>不放在 Web 启动链路</b>：建全文索引可能触发大表重写/填充，只挂在显式的初始化与同步动作里。
    /// </summary>
    public static class ProductFullTextIndexEnsurer
    {
        /// <summary>商品表名（与 Product 实体的 [SugarTable("mms_product")] 一致）</summary>
        private const string ProductTable = "mms_product";

        /// <summary>
        /// 商品名全文索引名（MySQL 建索引与就绪探测共用；SQL Server 索引名由系统生成，探测只看表）。
        /// 与 ProductService.IsProductNameFullTextReady 的探测口径必须一致。
        /// </summary>
        public const string MySqlIndexName = "ft_product_name";

        /// <summary>全文目录名（SQL Server；与文章全文检索共用同一个目录，避免多建）</summary>
        private const string FullTextCatalog = "ft_catalog";

        /// <summary>
        /// 确保商品名全文索引存在。返回可读的处理结果（供同步接口回显）；任何失败都不抛异常。
        /// </summary>
        public static string Ensure(ISqlSugarClient db)
        {
            if (db == null)
            {
                return "商城库连接为空，跳过商品名全文索引创建";
            }

            try
            {
                var dbType = db.CurrentConnectionConfig.DbType;
                if (dbType == SqlSugar.DbType.SqlServer)
                {
                    return EnsureSqlServer(db);
                }
                if (dbType == SqlSugar.DbType.MySql)
                {
                    return EnsureMySql(db);
                }

                return $"数据库 {dbType} 不支持列级全文索引，跳过（搜索将回落 LIKE）";
            }
            catch (Exception ex)
            {
                // 无 DDL 权限、实例未装组件、表不存在等都会落到这里：只告警，不影响初始化/同步的其它步骤
                Log.WriteLine(ConsoleColor.Yellow, $"[商城全文索引] 创建商品名全文索引失败（搜索将回落 LIKE）：{ex.Message}");
                return $"创建商品名全文索引失败：{ex.Message}";
            }
        }

        private static string EnsureSqlServer(ISqlSugarClient db)
        {
            // 实例没装"全文和语义提取搜索"组件时，建索引必失败，先探测再决定
            var installed = db.Ado.GetInt("SELECT CAST(SERVERPROPERTY('IsFullTextInstalled') AS int)");
            if (installed != 1)
            {
                return "SQL Server 实例未安装全文检索组件，跳过（搜索将回落 LIKE）";
            }

            if (db.Ado.GetInt($"SELECT COUNT(1) FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('{ProductTable}')") > 0)
            {
                return "商品名全文索引已存在，跳过";
            }

            // 目录：优先复用同名目录；没有则新建。库里已有其它默认目录时不能强制 AS DEFAULT（会冲突），
            // 所以建好后一律用 ON <目录> 显式指定，不依赖默认目录。
            db.Ado.ExecuteCommand(
                $"IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = '{FullTextCatalog}') " +
                $"BEGIN " +
                $"    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE is_default = 1) " +
                $"        CREATE FULLTEXT CATALOG {FullTextCatalog} AS DEFAULT; " +
                $"    ELSE " +
                $"        CREATE FULLTEXT CATALOG {FullTextCatalog}; " +
                $"END");

            // 全文索引的 KEY INDEX 必须是唯一、非空、单列索引 —— 取主键
            var pk = db.Ado.SqlQuery<string>(
                $"SELECT TOP 1 i.name FROM sys.indexes i WHERE i.object_id = OBJECT_ID('{ProductTable}') AND i.is_primary_key = 1")
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(pk))
            {
                return $"商品表 {ProductTable} 缺少主键（唯一非空单列索引），无法创建全文索引，跳过";
            }

            // LANGUAGE 2052 = 简体中文分词；CHANGE_TRACKING AUTO 让商品增改自动同步索引，无需任务维护
            db.Ado.ExecuteCommand(
                $"CREATE FULLTEXT INDEX ON {ProductTable}([ProductName] LANGUAGE 2052) " +
                $"KEY INDEX [{pk}] ON {FullTextCatalog} WITH CHANGE_TRACKING AUTO");

            Log.WriteLine(ConsoleColor.Green, $"[商城全文索引] 已创建商品名全文索引（表={ProductTable} 目录={FullTextCatalog} 主键={pk}）");
            return "已创建商品名全文索引（首次填充可能需一点时间）";
        }

        private static string EnsureMySql(ISqlSugarClient db)
        {
            var exists = db.Ado.GetInt(
                "SELECT COUNT(1) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND INDEX_NAME = @index",
                new { table = ProductTable, index = MySqlIndexName });
            if (exists > 0)
            {
                return "商品名全文索引已存在，跳过";
            }

            // 必须 WITH PARSER ngram，否则按空格分词、中文基本搜不出；大表会重建，建议低峰执行
            db.Ado.ExecuteCommand(
                $"ALTER TABLE {ProductTable} ADD FULLTEXT INDEX {MySqlIndexName} (ProductName) WITH PARSER ngram");

            Log.WriteLine(ConsoleColor.Green, $"[商城全文索引] 已创建商品名全文索引（表={ProductTable} 索引={MySqlIndexName}）");
            return "已创建商品名全文索引（大表重建可能较慢）";
        }
    }
}
