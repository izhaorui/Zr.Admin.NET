namespace ZR.Model.System
{
    /// <summary>
    /// 数据库迁移历史记录
    /// </summary>
    [SugarTable("__db_migration_history", "数据库迁移历史")]
    [Tenant("0")]
    public class DbMigrationHistory
    {
        /// <summary>主键</summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>迁移批次号(时间戳 + 随机)</summary>
        [SugarColumn(Length = 32)]
        public string BatchId { get; set; }

        /// <summary>变更摘要</summary>
        [SugarColumn(Length = 500)]
        public string Summary { get; set; }

        /// <summary>变更详情(JSON)：新增表列表、新增列列表、失败实体</summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString)]
        public string Details { get; set; }

        /// <summary>迁移时间</summary>
        public DateTime AppliedAt { get; set; }

        /// <summary>新增表数量</summary>
        public int NewTables { get; set; }

        /// <summary>新增列数量</summary>
        public int NewColumns { get; set; }

        /// <summary>是否成功</summary>
        public bool Success { get; set; }

        /// <summary>错误信息（成功时为空）</summary>
        [SugarColumn(Length = 4000, IsNullable = true)]
        public string Error { get; set; }
    }
}
