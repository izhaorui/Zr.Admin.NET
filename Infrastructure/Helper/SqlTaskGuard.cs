using System;
using System.Text.RegularExpressions;

namespace ZR.Infrastructure.Helper
{
    /// <summary>
    /// SQL(T-SQL) 执行安全护栏，供定时任务等「运行时执行外部配置 SQL」的场景复用。
    /// 允许单条写操作(INSERT/UPDATE/DELETE/MERGE)，但必须禁止：
    /// 多语句(;)、批处理(GO)、DDL/危险操作(drop/alter/create/truncate/grant...)、系统存储过程(xp_/sp_)、
    /// 以及无 WHERE 的全表 UPDATE/DELETE。
    /// 校验前先去除字符串字面量与注释，再做关键字/条件边界检测，避免误伤正常列名/表名。
    /// </summary>
    public static class SqlTaskGuard
    {
        // 危险关键字（小写，整词匹配）。命中任一即拒绝。
        private static readonly string[] ForbiddenKeywords =
        {
            "drop", "alter", "create", "truncate", "rename", "grant", "revoke",
            "exec", "execute", "attach", "backup", "restore",
            "declare", "waitfor", "shutdown"
        };

        // 危险前缀（系统存储过程）。前缀后必然紧跟字母，不能用整词匹配，否则永远命中不到。
        private static readonly string[] ForbiddenPrefixes =
        {
            "xp_", "sp_"
        };

        /// <summary>
        /// 校验 SQL 是否可安全执行（允许 SELECT/INSERT/UPDATE/DELETE/MERGE 单条语句）。
        /// </summary>
        /// <param name="sql">待校验 SQL</param>
        /// <param name="reason">不通过时的原因</param>
        public static bool IsSafe(string sql, out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(sql))
            {
                reason = "SQL 为空";
                return false;
            }

            // 1. 先剥离字符串字面量，再剥离注释，得到「可执行骨架」；
            //    T-SQL 中注释等价于空白，剥离后既能避免 `'a;drop'` 之类误伤，也能防止通过注释藏匿/切断关键字。
            var skeleton = StripComments(StripLiterals(sql));
            var normalized = Regex.Replace(skeleton, @"\s+", " ").Trim();

            // 2. 禁止 GO 批处理分隔（独立词，大小写不敏感）
            if (Regex.IsMatch(normalized, @"\bgo\b", RegexOptions.IgnoreCase))
            {
                reason = "不允许 GO 批处理分隔符";
                return false;
            }

            // 3. 禁止多语句（分号分隔）
            if (normalized.Contains(';'))
            {
                reason = "不允许多条语句(;分隔)";
                return false;
            }

            // 4. 禁止危险整词关键字
            foreach (var kw in ForbiddenKeywords)
            {
                if (Regex.IsMatch(normalized, $@"\b{Regex.Escape(kw)}\b", RegexOptions.IgnoreCase))
                {
                    reason = $"包含禁止的关键字: {kw}";
                    return false;
                }
            }

            // 5. 禁止系统存储过程前缀（仅需前边界，如 xp_cmdshell / sp_executesql）
            foreach (var prefix in ForbiddenPrefixes)
            {
                if (Regex.IsMatch(normalized, $@"\b{Regex.Escape(prefix)}", RegexOptions.IgnoreCase))
                {
                    reason = $"包含禁止的系统存储过程前缀: {prefix}";
                    return false;
                }
            }

            // 6. 禁止无 WHERE 的全表 UPDATE/DELETE（顶层语句判定，子查询中的 WHERE 不算数）
            if (IsFullTableWrite(normalized, out var stmt))
            {
                reason = $"{stmt} 未包含顶层 WHERE 条件，禁止全表写操作";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 去掉字符串字面量（含 '' 转义），保留其余结构。
        /// </summary>
        private static string StripLiterals(string sql)
        {
            return Regex.Replace(sql, "'(?:[^']|'')*'", "''", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// 去掉块注释(支持近似嵌套)与行注释，替换为空白以保持词元间距。
        /// </summary>
        private static string StripComments(string sql)
        {
            var blockComment = new Regex(@"/\*.*?\*/", RegexOptions.Singleline);
            var previous = sql;
            // 迭代处理（近似）嵌套块注释，直到不再变化
            while (blockComment.IsMatch(previous))
            {
                previous = blockComment.Replace(previous, " ");
            }

            // 行注释：-- 到行尾
            return Regex.Replace(previous, @"--[^\r\n]*", " ");
        }

        /// <summary>
        /// 判断是否为「无顶层 WHERE 的 UPDATE/DELETE」。
        /// </summary>
        private static bool IsFullTableWrite(string normalized, out string statement)
        {
            statement = null;
            var match = Regex.Match(normalized, @"^(delete|update)\b", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return false;
            }

            statement = match.Groups[1].Value.ToUpperInvariant();
            // 顶层无 WHERE 即视为全表写操作
            return !HasTopLevelKeyword(normalized, "where");
        }

        /// <summary>
        /// 在括号深度为 0（顶层）处查找关键字，避免把子查询里的 WHERE 当作保护条件。
        /// </summary>
        private static bool HasTopLevelKeyword(string sql, string keyword)
        {
            var depth = 0;
            for (var i = 0; i < sql.Length; i++)
            {
                var c = sql[i];
                if (c == '(')
                {
                    depth++;
                    continue;
                }
                if (c == ')')
                {
                    if (depth > 0) depth--;
                    continue;
                }
                if (depth != 0 || !char.IsLetter(c))
                {
                    continue;
                }

                // 词起始边界
                if (i > 0 && IsWordChar(sql[i - 1]))
                {
                    continue;
                }

                var end = i + keyword.Length;
                if (end > sql.Length)
                {
                    continue;
                }
                if (string.Compare(sql, i, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    continue;
                }

                // 词结束边界
                if (end < sql.Length && IsWordChar(sql[end]))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
