using System.Text.RegularExpressions;

namespace ZR.Tasks.TaskScheduler
{
    /// <summary>
    /// 定时任务 SQL 安全护栏。
    /// 定时任务(SysTasks.SqlText)由已授权运维人员配置，设计上需要执行写操作(INSERT/UPDATE/DELETE)，
    /// 但必须禁止：多语句(;)、批处理(GO)、DDL/危险操作(drop/alter/create/truncate/grant...)、系统存储过程(xp_/sp_)。
    /// 通过去除字符串字面量后再做关键字边界检测，避免误伤正常列名/表名。
    /// </summary>
    internal static class SqlTaskGuard
    {
        // 危险关键字（小写）。命中任一即拒绝。
        private static readonly string[] ForbiddenKeywords =
        {
            "drop", "alter", "create", "truncate", "rename", "grant", "revoke",
            "exec", "execute", "xp_", "sp_", "attach", "backup", "restore",
            "declare", "waitfor", "shutdown"
        };

        /// <summary>
        /// 校验定时任务 SQL 是否可安全执行（允许 SELECT/INSERT/UPDATE/DELETE/MERGE 单条语句）。
        /// </summary>
        internal static bool IsSafe(string sql, out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(sql))
            {
                reason = "SQL 为空";
                return false;
            }

            var normalized = sql.Trim().Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");

            // 1. 禁止 GO 批处理分隔（独立词，大小写不敏感）
            if (Regex.IsMatch(normalized, @"\bgo\b", RegexOptions.IgnoreCase))
            {
                reason = "不允许 GO 批处理分隔符";
                return false;
            }

            // 去除字符串字面量后再做后续检测，避免 'a;drop' 这类内容误伤
            var stripped = Regex.Replace(normalized, "'(?:[^']|'')*'", "''", RegexOptions.IgnoreCase);

            // 2. 禁止多语句（分号分隔）
            if (stripped.Contains(';'))
            {
                reason = "不允许多条语句(;分隔)";
                return false;
            }

            // 3. 禁止危险关键字（带词边界判断）
            var lower = stripped.ToLowerInvariant();
            foreach (var kw in ForbiddenKeywords)
            {
                var idx = lower.IndexOf(kw, System.StringComparison.Ordinal);
                if (idx < 0) continue;

                var before = idx > 0 ? lower[idx - 1] : ' ';
                var after = idx + kw.Length < lower.Length ? lower[idx + kw.Length] : ' ';
                var isWordBoundary = !char.IsLetterOrDigit(before) && !char.IsLetterOrDigit(after);
                if (isWordBoundary)
                {
                    reason = $"包含禁止的关键字: {kw}";
                    return false;
                }
            }

            return true;
        }
    }
}
