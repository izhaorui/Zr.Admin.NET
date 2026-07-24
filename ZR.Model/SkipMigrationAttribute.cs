namespace ZR.Model
{
    /// <summary>
    /// 标记实体不参与自动数据库迁移（CodeFirst.InitTables）。
    /// 适用于：用户自定义业务表、手工维护 DDL 的表、不想被自动改结构的表。
    /// 用法：[SkipMigration] 或 [SkipMigration("手动维护，不自动迁移")]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class SkipMigrationAttribute : Attribute
    {
        /// <summary>跳过原因说明（可选）</summary>
        public string Reason { get; }

        public SkipMigrationAttribute() { }

        public SkipMigrationAttribute(string reason)
        {
            Reason = reason;
        }
    }
}
