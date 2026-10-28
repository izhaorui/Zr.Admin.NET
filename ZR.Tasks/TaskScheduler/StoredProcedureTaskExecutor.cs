using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Infrastructure;
using SqlSugar;
using SqlSugar.IOC;
using ZR.Model.System;

namespace ZR.Tasks.TaskScheduler
{
    /// <summary>
    /// Executes SQL tasks through schema-qualified stored procedures with parameterized arguments.
    /// </summary>
    public static class StoredProcedureTaskExecutor
    {
        private const int MaxParameterCount = 100;
        private static readonly Regex ProcedureNamePattern = new(
            @"^(?:[A-Za-z_][A-Za-z0-9_$#]*\.){1,2}[A-Za-z_][A-Za-z0-9_$#]*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ProcedureCallPattern = new(
            @"^(?<name>(?:[A-Za-z_][A-Za-z0-9_$#]*\.){1,2}[A-Za-z_][A-Za-z0-9_$#]*)(?:\s+(?<parameters>[\s\S]+))?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ParameterNamePattern = new(
            @"^@?[A-Za-z_][A-Za-z0-9_]*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool IsProcedureCall(string sqlText)
        {
            return ProcedureCallPattern.IsMatch(sqlText?.Trim() ?? string.Empty);
        }

        public static void ValidateConfiguration(string procedureCall, string legacyParameters)
        {
            ParseProcedureCall(procedureCall, legacyParameters);
        }

        public static bool IsValidProcedureName(string procedureName)
        {
            var name = procedureName?.Trim();
            return !string.IsNullOrEmpty(name) && ProcedureNamePattern.IsMatch(name);
        }

        public static int Execute(SysTasks task, string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new CustomException($"任务{task.ID}执行失败，租户标识为空");
            }

            var (procedureName, parameters) = ParseProcedureCall(task.SqlText, task.JobParams);
            return DbScoped.SugarScope.GetConnectionScope(tenantId)
                .Ado.UseStoredProcedure()
                .ExecuteCommand(procedureName, parameters.ToArray());
        }

        private static void EnsureValidProcedureName(string procedureName)
        {
            if (!IsValidProcedureName(procedureName))
            {
                throw new CustomException(
                    $"存储过程名称格式无效，必须使用 schema.ProcedureName 或 database.schema.ProcedureName：{procedureName}");
            }
        }

        private static (string ProcedureName, List<SugarParameter> Parameters) ParseProcedureCall(
            string procedureCall,
            string legacyParameters)
        {
            var match = ProcedureCallPattern.Match(procedureCall?.Trim() ?? string.Empty);
            if (!match.Success)
            {
                throw new CustomException(
                    "存储过程格式无效，请输入 schema.ProcedureName 或 database.schema.ProcedureName，并可在后面填写参数，例如 dbo.RefreshTaskData @usid = 1");
            }

            var procedureName = match.Groups["name"].Value;
            EnsureValidProcedureName(procedureName);

            var inlineParameters = match.Groups["parameters"].Success
                ? match.Groups["parameters"].Value
                : null;
            if (!string.IsNullOrWhiteSpace(inlineParameters) && !string.IsNullOrWhiteSpace(legacyParameters))
            {
                throw new CustomException("存储过程参数请统一填写在存储过程输入框中");
            }

            return (procedureName, ParseParameters(inlineParameters ?? legacyParameters));
        }

        private static List<SugarParameter> ParseParameters(string parameterText)
        {
            if (string.IsNullOrWhiteSpace(parameterText))
            {
                return new List<SugarParameter>();
            }

            var input = parameterText.Trim();
            return input.StartsWith('{')
                ? ParseJsonParameters(input)
                : ParseAssignmentParameters(input);
        }

        private static List<SugarParameter> ParseJsonParameters(string parametersJson)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(parametersJson, new JsonDocumentOptions
                {
                    MaxDepth = 8
                });
            }
            catch (JsonException ex)
            {
                throw new CustomException($"存储过程参数不是有效的 JSON：{ex.Message}");
            }

            using (document)
            {
                return ParseParameters(document.RootElement);
            }
        }

        private static List<SugarParameter> ParseAssignmentParameters(string parameterText)
        {
            var parameters = new List<SugarParameter>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var assignment in SplitAssignments(parameterText))
            {
                var separator = FindAssignmentSeparator(assignment);
                if (separator <= 0)
                {
                    throw new CustomException($"存储过程参数格式无效，应为 @name=value：{assignment}");
                }

                var name = assignment[..separator].Trim();
                if (!ParameterNamePattern.IsMatch(name))
                {
                    throw new CustomException($"存储过程参数名称无效：{name}");
                }

                var normalizedName = name.TrimStart('@');
                if (!names.Add(normalizedName))
                {
                    throw new CustomException($"存储过程参数名称重复：{normalizedName}");
                }

                if (parameters.Count >= MaxParameterCount)
                {
                    throw new CustomException($"存储过程参数不能超过 {MaxParameterCount} 个");
                }

                var value = assignment[(separator + 1)..].Trim();
                parameters.Add(new SugarParameter($"@{normalizedName}", ParseAssignmentValue(value)));
            }

            return parameters;
        }

        private static List<string> SplitAssignments(string parameterText)
        {
            var assignments = new List<string>();
            var start = 0;
            char quote = '\0';

            for (var i = 0; i < parameterText.Length; i++)
            {
                var current = parameterText[i];
                if (quote == '\'')
                {
                    if (current == '\'' && i + 1 < parameterText.Length && parameterText[i + 1] == '\'')
                    {
                        i++;
                    }
                    else if (current == '\'')
                    {
                        quote = '\0';
                    }
                }
                else if (quote == '"')
                {
                    if (current == '\\')
                    {
                        i++;
                    }
                    else if (current == '"')
                    {
                        quote = '\0';
                    }
                }
                else if (current == '\'' || current == '"')
                {
                    quote = current;
                }
                else if (current == ',')
                {
                    AddAssignment(assignments, parameterText[start..i]);
                    start = i + 1;
                }
            }

            if (quote != '\0')
            {
                throw new CustomException("存储过程参数字符串引号未闭合");
            }

            AddAssignment(assignments, parameterText[start..]);
            return assignments;
        }

        private static void AddAssignment(List<string> assignments, string assignment)
        {
            var trimmed = assignment.Trim();
            if (trimmed.Length == 0)
            {
                throw new CustomException("存储过程参数中存在空项");
            }

            assignments.Add(trimmed);
        }

        private static int FindAssignmentSeparator(string assignment)
        {
            var quote = '\0';
            for (var i = 0; i < assignment.Length; i++)
            {
                var current = assignment[i];
                if (quote == '\'')
                {
                    if (current == '\'' && i + 1 < assignment.Length && assignment[i + 1] == '\'')
                    {
                        i++;
                    }
                    else if (current == '\'')
                    {
                        quote = '\0';
                    }
                }
                else if (quote == '"')
                {
                    if (current == '\\')
                    {
                        i++;
                    }
                    else if (current == '"')
                    {
                        quote = '\0';
                    }
                }
                else if (current == '\'' || current == '"')
                {
                    quote = current;
                }
                else if (current == '=')
                {
                    return i;
                }
            }

            return -1;
        }

        private static object ParseAssignmentValue(string value)
        {
            if (value.StartsWith('\'') || value.EndsWith('\''))
            {
                if (value.Length < 2 || value[0] != '\'' || value[^1] != '\'')
                {
                    throw new CustomException("存储过程参数字符串引号不匹配");
                }

                return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
            }

            if (value.StartsWith('"') || value.EndsWith('"'))
            {
                if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
                {
                    throw new CustomException("存储过程参数字符串引号不匹配");
                }

                try
                {
                    return JsonSerializer.Deserialize<string>(value);
                }
                catch (JsonException ex)
                {
                    throw new CustomException($"存储过程参数字符串格式无效：{ex.Message}");
                }
            }

            if (value.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                return DBNull.Value;
            }

            if (bool.TryParse(value, out var boolean))
            {
                return boolean;
            }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            {
                return integer;
            }

            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            {
                return number;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatingPoint))
            {
                return floatingPoint;
            }

            if (value.Length == 0)
            {
                throw new CustomException("存储过程参数值不能为空；字符串请使用单引号或双引号");
            }

            return value;
        }

        private static List<SugarParameter> ParseParameters(JsonElement root)
        {
            var parameters = new List<SugarParameter>();
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new CustomException("存储过程参数必须是 JSON 对象");
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in root.EnumerateObject())
            {
                var name = property.Name.Trim();
                if (!ParameterNamePattern.IsMatch(name))
                {
                    throw new CustomException($"存储过程参数名称无效：{property.Name}");
                }

                var normalizedName = name.TrimStart('@');
                if (!names.Add(normalizedName))
                {
                    throw new CustomException($"存储过程参数名称重复：{normalizedName}");
                }

                if (parameters.Count >= MaxParameterCount)
                {
                    throw new CustomException($"存储过程参数不能超过 {MaxParameterCount} 个");
                }

                parameters.Add(new SugarParameter($"@{normalizedName}", ParseParameterValue(property.Value)));
            }

            return parameters;
        }

        private static object ParseParameterValue(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
                JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
                JsonValueKind.Number => value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => DBNull.Value,
                _ => throw new CustomException("存储过程参数仅支持字符串、数字、布尔值或 null")
            };
        }
    }
}
