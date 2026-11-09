using Infrastructure;
using Infrastructure.Extensions;
using Infrastructure.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZR.CodeGenerator.Model;
using ZR.Model.System.Generate;

namespace ZR.CodeGenerator
{
    /// <summary>
    /// 基于代码生成配置构造 AI 可读契约（结构化 JSON + Markdown）。
    /// 复用 CodeGeneratorTool 的选项推导逻辑，保证描述与真实生成结果一致；
    /// 只读取配置，不写库、不渲染模板、不影响原有生成流程。
    /// </summary>
    public static class CodeGenSpecBuilder
    {
        /// <summary>
        /// 构建契约
        /// </summary>
        /// <param name="genTable">代码生成配置（需已填充 Columns / SubTables）</param>
        /// <param name="dbType">SqlSugar DbType 值</param>
        public static CodeGenSpec Build(GenTable genTable, int dbType)
        {
            if (genTable == null) throw new ArgumentNullException(nameof(genTable));

            var replaceDto = CodeGeneratorTool.BuildReplaceDto(genTable);
            var genOptions = CodeGeneratorTool.GenerateOption(genTable);
            var options = genTable.Options ?? new CodeOptions();
            var columns = genTable.Columns ?? new List<GenTableColumn>();
            var apis = BuildApis(genTable, replaceDto, columns);

            return new CodeGenSpec
            {
                Table = new SpecTable
                {
                    TableName = genTable.TableName,
                    TableComment = genTable.TableComment,
                    DbName = genTable.DbName,
                    DbType = GetDbTypeName(dbType),
                    TplCategory = genTable.TplCategory ?? GenConstants.TPL_CRUD,
                    TplCategoryText = GetTplCategoryText(genTable.TplCategory),
                    ClassName = replaceDto.ModelTypeName,
                    ModuleName = genTable.ModuleName,
                    BusinessName = genTable.BusinessName,
                    FunctionName = genTable.FunctionName,
                    BaseNameSpace = genTable.BaseNameSpace,
                    FunctionAuthor = genTable.FunctionAuthor
                },
                Options = options,
                Namespaces = new SpecNamespaces
                {
                    Model = $"{genOptions.ModelsNamespace}.{genOptions.SubNamespace}",
                    Dto = $"{genOptions.DtosNamespace}.{genOptions.SubNamespace}.Dto",
                    Service = $"{genOptions.ServicesNamespace}.{genOptions.SubNamespace}",
                    IService = $"{genOptions.IServicsNamespace}.{genOptions.SubNamespace}.I{genOptions.SubNamespace}Service",
                    Repository = $"{genOptions.RepositoriesNamespace}.{genOptions.SubNamespace}",
                    Controller = $"{genOptions.ApiControllerNamespace}.Controllers.{genOptions.SubNamespace}"
                },
                TargetFiles = BuildTargetFiles(genTable, replaceDto, genOptions, options),
                Columns = columns.Select(ToSpecColumn).ToList(),
                SubTables = BuildSubTables(genTable),
                Apis = apis,
                Permissions = BuildPermissions(genTable, replaceDto, apis),
                Features = new SpecFeatures
                {
                    PrimaryKey = replaceDto.PKName,
                    PrimaryKeyType = replaceDto.PKType,
                    UseSnowflakeId = options.UseSnowflakeId,
                    SoftDelete = replaceDto.EnableSoftDelete,
                    SoftDeleteField = replaceDto.SoftDeleteField,
                    SoftDeleteFieldType = replaceDto.SoftDeleteFieldType,
                    SoftDeleteNormalValue = replaceDto.SoftDeleteNormalValue,
                    SoftDeleteDeletedValue = replaceDto.SoftDeleteDeletedValue,
                    EnableLog = options.EnableLog,
                    HasUploadFile = replaceDto.UploadFile == 1,
                    HasMultiSelect = replaceDto.SelectMulti == 1,
                    HasEditor = replaceDto.ShowEditor == 1,
                    HasCustomInput = columns.Any(f => GenConstants.HTML_CUSTOM_INPUT.Equals(f.HtmlType, StringComparison.OrdinalIgnoreCase))
                },
                Conventions = BuildConventions()
            };
        }

        /// <summary>
        /// 渲染为 Markdown，便于直接复制给 AI 编码助手
        /// </summary>
        public static string ToMarkdown(CodeGenSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));

            var t = spec.Table;
            var f = spec.Features;
            var sb = new StringBuilder();

            sb.AppendLine($"# 代码生成契约：{Cell(t.TableComment)}（{t.TableName}）");
            sb.AppendLine();
            sb.AppendLine("> 由 ZRAdminNet 代码生成器导出，用于让 AI / 编码助手按本项目既有约定实现代码。");
            sb.AppendLine("> 契约只描述“应该长什么样”；若与项目现有代码冲突，以现有代码为准。表格中 `✓` 表示启用。");
            sb.AppendLine();

            sb.AppendLine("## 1. 表信息");
            sb.AppendLine("| 项 | 值 |");
            sb.AppendLine("| --- | --- |");
            sb.AppendLine($"| 表名 | `{t.TableName}` |");
            sb.AppendLine($"| 中文名 | {Cell(t.TableComment)} |");
            sb.AppendLine($"| 数据库 | `{t.DbName}`（{t.DbType}） |");
            sb.AppendLine($"| 模板类型 | `{t.TplCategory}`（{t.TplCategoryText}） |");
            sb.AppendLine($"| 实体类名 | `{t.ClassName}` |");
            sb.AppendLine($"| 模块名 / 业务名 | `{t.ModuleName}` / `{t.BusinessName}` |");
            sb.AppendLine($"| 功能名 | {Cell(t.FunctionName)} |");
            sb.AppendLine($"| 命名空间前缀 | `{t.BaseNameSpace}` |");
            sb.AppendLine();

            sb.AppendLine("## 2. 命名与命名空间");
            sb.AppendLine("| 层 | 命名空间 |");
            sb.AppendLine("| --- | --- |");
            sb.AppendLine($"| 实体 | `{spec.Namespaces.Model}` |");
            sb.AppendLine($"| Dto | `{spec.Namespaces.Dto}` |");
            sb.AppendLine($"| 服务接口 | `{spec.Namespaces.IService}` |");
            sb.AppendLine($"| 服务实现 | `{spec.Namespaces.Service}` |");
            sb.AppendLine($"| 控制器 | `{spec.Namespaces.Controller}` |");
            if (spec.Options?.GenerateRepo == 1)
            {
                sb.AppendLine($"| 仓储 | `{spec.Namespaces.Repository}` |");
            }
            sb.AppendLine();

            sb.AppendLine("## 3. 字段定义");
            sb.AppendLine("| 列名 | C# 字段 | C# 类型 | 库类型 | 中文名 | 键 | 必填 | 编辑 | 列表 | 查询 | 查询方式 | 控件 | 字典 | 导出 |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var c in spec.Columns)
            {
                var key = c.IsPk && c.IsIncrement ? "PK·自增"
                    : c.IsPk ? "PK"
                    : c.IsIncrement ? "自增" : "";
                sb.AppendLine($"| `{c.ColumnName}` | `{c.CsharpField}` | `{c.CsharpType}` | `{c.ColumnType}` | {Cell(c.Comment)} | {key} | {Mark(c.IsRequired)} | {Mark(c.IsEdit)} | {Mark(c.IsList)} | {Mark(c.IsQuery)} | `{c.QueryType}` | `{c.HtmlType}` | {Cell(c.DictType)} | {Mark(c.IsExport)} |");
            }
            sb.AppendLine();
            var dictColumns = spec.Columns.Where(c => !string.IsNullOrEmpty(c.DictType)).ToList();
            if (dictColumns.Count > 0)
            {
                sb.AppendLine("字典字段（前端需按字典渲染并加载字典数据）：");
                foreach (var c in dictColumns)
                {
                    sb.AppendLine($"- `{c.CsharpField}` → 字典 `{c.DictType}`");
                }
                sb.AppendLine();
            }

            sb.AppendLine("## 4. 行为开关");
            sb.AppendLine("| 开关 | 值 |");
            sb.AppendLine("| --- | --- |");
            sb.AppendLine($"| 主键 | `{f.PrimaryKey}`（{f.PrimaryKeyType}） |");
            sb.AppendLine($"| 主键策略 | {(f.UseSnowflakeId ? "雪花 ID（应用层生成，非数据库自增）" : "数据库自增 / 业务指定")} |");
            sb.AppendLine($"| 软删除 | {(f.SoftDelete ? $"`{f.SoftDeleteField}`：正常 `{f.SoftDeleteNormalValue}`，已删除 `{f.SoftDeleteDeletedValue}`（删除走更新，查询需过滤）" : "未启用（物理删除）")} |");
            sb.AppendLine($"| 操作日志 | {(f.EnableLog ? "启用在服务层自动记录数据变更" : "未启用")} |");
            sb.AppendLine($"| 文件上传 | {(f.HasUploadFile ? "有字段使用图片/文件上传控件" : "无")} |");
            sb.AppendLine($"| 下拉多选 | {(f.HasMultiSelect ? "有（查询参数需数组序列化，如 QS.stringify indices=false）" : "无")} |");
            sb.AppendLine($"| 富文本 | {(f.HasEditor ? "有字段使用富文本编辑器" : "无")} |");
            sb.AppendLine($"| 行内排序 | {(f.HasCustomInput ? "有（需要 ChangeSort 接口按 id 改排序值）" : "无")} |");
            sb.AppendLine($"| 前端模板 | {GetFrontTplText(spec.Options?.FrontTpl ?? 0)} |");
            sb.AppendLine($"| 显隐列 | {(spec.Options?.EnableColumns == true ? "启用" : "不启用")} |");
            sb.AppendLine();

            sb.AppendLine("## 5. 权限与菜单");
            sb.AppendLine($"- 权限前缀：`{spec.Options?.PermissionPrefix}`");
            sb.AppendLine($"- 菜单：类型 `C`，path `{t.BusinessName}`，component `{t.ModuleName?.FirstLowerCase()}/{t.BusinessName}`，父级菜单 id `{spec.Options?.ParentMenuId ?? 0}`");
            sb.AppendLine();
            sb.AppendLine("| 权限码 | 用途 |");
            sb.AppendLine("| --- | --- |");
            foreach (var p in spec.Permissions)
            {
                sb.AppendLine($"| `{p.Code}` | {Cell(p.Name)} |");
            }
            sb.AppendLine();

            sb.AppendLine("## 6. 接口契约");
            sb.AppendLine("| Method | Route | Action | 权限码 |");
            sb.AppendLine("| --- | --- | --- | --- |");
            foreach (var a in spec.Apis)
            {
                sb.AppendLine($"| {a.Method} | `{a.Route}` | `{a.Action}` | `{a.Permission}` |");
            }
            sb.AppendLine();

            sb.AppendLine("## 7. 目标文件");
            sb.AppendLine("| 层 | 相对路径 |");
            sb.AppendLine("| --- | --- |");
            foreach (var file in spec.TargetFiles)
            {
                sb.AppendLine($"| {file.Layer} | `{file.Path}` |");
            }
            sb.AppendLine();

            if (spec.SubTables.Count > 0)
            {
                sb.AppendLine("## 8. 子表");
                foreach (var sub in spec.SubTables)
                {
                    sb.AppendLine($"### {Cell(sub.TableComment)}（`{sub.TableName}` → `{sub.ClassName}`，外键 `{sub.ParentFkName}`）");
                    sb.AppendLine("| 列名 | C# 字段 | C# 类型 | 库类型 | 中文名 | 必填 | 编辑 | 列表 | 控件 | 字典 |");
                    sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
                    foreach (var c in sub.Columns)
                    {
                        sb.AppendLine($"| `{c.ColumnName}` | `{c.CsharpField}` | `{c.CsharpType}` | `{c.ColumnType}` | {Cell(c.Comment)} | {Mark(c.IsRequired)} | {Mark(c.IsEdit)} | {Mark(c.IsList)} | `{c.HtmlType}` | {Cell(c.DictType)} |");
                    }
                    sb.AppendLine();
                }
            }

            sb.AppendLine($"## {(spec.SubTables.Count > 0 ? 9 : 8)}. 项目约定（必须遵守）");
            foreach (var item in spec.Conventions)
            {
                sb.AppendLine($"- {item}");
            }
            sb.AppendLine();

            return sb.ToString();
        }

        #region 构建各段

        private static List<SpecColumn> BuildColumns(IEnumerable<GenTableColumn> columns)
        {
            return (columns ?? Enumerable.Empty<GenTableColumn>()).Select(ToSpecColumn).ToList();
        }

        private static SpecColumn ToSpecColumn(GenTableColumn column)
        {
            return new SpecColumn
            {
                ColumnName = column.ColumnName,
                CsharpField = column.CsharpField,
                CsharpType = column.CsharpType,
                ColumnType = column.ColumnType,
                Comment = column.ColumnComment,
                Remark = column.Remark,
                IsPk = column.IsPk,
                IsIncrement = column.IsIncrement,
                IsRequired = column.IsRequired,
                IsInsert = column.IsInsert,
                IsEdit = column.IsEdit,
                IsList = column.IsList,
                IsQuery = column.IsQuery,
                IsSort = column.IsSort,
                IsExport = column.IsExport,
                HtmlType = column.HtmlType,
                QueryType = column.QueryType,
                DictType = column.DictType,
                AutoFillType = column.AutoFillType,
                Sort = column.Sort
            };
        }

        private static List<SpecSubTable> BuildSubTables(GenTable genTable)
        {
            var subs = genTable.SubTables;
            if ((subs == null || subs.Count == 0) && genTable.SubTable != null)
            {
                subs = new List<GenTable> { genTable.SubTable };
            }
            if (subs == null) return new List<SpecSubTable>();

            return subs
                .Where(f => f != null && !string.IsNullOrEmpty(f.ClassName))
                .Select(f => new SpecSubTable
                {
                    TableName = f.TableName,
                    ClassName = f.ClassName,
                    TableComment = f.TableComment,
                    ParentFkName = f.ParentFkName,
                    Columns = BuildColumns(f.Columns)
                })
                .ToList();
        }

        /// <summary>
        /// 生成的目标文件，路径规则与 CodeGeneratorTool 的 Generate* 方法保持一致
        /// </summary>
        private static List<SpecFile> BuildTargetFiles(GenTable genTable, ReplaceDto replaceDto, CodeGenerateOption genOptions, CodeOptions options)
        {
            var name = replaceDto.ModelTypeName;
            var module = genTable.ModuleName ?? string.Empty;
            var business = genTable.BusinessName ?? string.Empty;

            var files = new List<SpecFile>
            {
                new() { Layer = "实体 Model", Path = $"{genOptions.ModelsNamespace}/{genOptions.SubNamespace}/{name}.cs" },
                new() { Layer = "Dto", Path = $"{genOptions.ModelsNamespace}/{genOptions.SubNamespace}/Dto/{name}Dto.cs" },
                new() { Layer = "服务接口", Path = $"{genOptions.IServicsNamespace}/{genOptions.SubNamespace}/I{genOptions.SubNamespace}Service/I{name}Service.cs" },
                new() { Layer = "服务实现", Path = $"{genOptions.ServicesNamespace}/{genOptions.SubNamespace}/{name}Service.cs" },
                new() { Layer = "控制器", Path = $"{genOptions.ApiControllerNamespace}/Controllers/{genOptions.SubNamespace}/{name}Controller.cs" }
            };
            if (options.GenerateRepo == 1)
            {
                files.Add(new SpecFile { Layer = "仓储", Path = $"{genOptions.RepositoriesNamespace}/{genOptions.SubNamespace}/{name}Repository.cs" });
            }

            // 前端路径与前端的生成配置一致，空值默认 Frontend
            var vueRoot = AppSettings.Get<CodeGen>("codeGen")?.VuePath;
            if (string.IsNullOrEmpty(vueRoot)) vueRoot = "Frontend";
            var apiFileName = options.FrontTpl == 1 ? business.FirstLowerCase() : business.ToLower();

            // 三种前端模板（Vue2 / Vue3 / AntDesign）都会产出页面，路径规则一致
            files.Add(new SpecFile { Layer = "前端页面", Path = $"{vueRoot}/src/views/{module.FirstLowerCase()}/{replaceDto.ViewFileName}.vue" });
            files.Add(new SpecFile { Layer = "前端 API", Path = $"{vueRoot}/src/api/{module.FirstLowerCase()}/{apiFileName}.js" });
            files.Add(new SpecFile { Layer = "菜单权限 SQL", Path = $"sql/{business}.sql" });

            return files;
        }

        /// <summary>
        /// 接口契约，与 TplControllers.txt 保持一致
        /// </summary>
        private static List<SpecApi> BuildApis(GenTable genTable, ReplaceDto replaceDto, List<GenTableColumn> columns)
        {
            var prefix = replaceDto.PermissionPrefix;
            var name = replaceDto.ModelTypeName;
            var route = $"/{genTable.ModuleName}/{genTable.BusinessName}";
            var apis = new List<SpecApi>
            {
                new() { Method = "GET", Route = $"{route}/list", Action = $"Query{name}", Permission = $"{prefix}:list" }
            };

            if (GenConstants.TPL_TREE.Equals(genTable.TplCategory, StringComparison.OrdinalIgnoreCase))
            {
                apis.Add(new SpecApi { Method = "GET", Route = $"{route}/treeList", Action = $"QueryTree{name}", Permission = $"{prefix}:list" });
            }

            apis.Add(new SpecApi { Method = "GET", Route = $"{route}/{{{replaceDto.PKName}}}", Action = $"Get{name}", Permission = $"{prefix}:query" });

            if (replaceDto.ShowBtnAdd)
            {
                apis.Add(new SpecApi { Method = "POST", Route = route, Action = $"Add{name}", Permission = $"{prefix}:add" });
            }
            if (replaceDto.ShowBtnEdit)
            {
                apis.Add(new SpecApi { Method = "PUT", Route = route, Action = $"Update{name}", Permission = $"{prefix}:edit" });
            }
            if (replaceDto.ShowBtnDelete || replaceDto.ShowBtnMultiDel)
            {
                apis.Add(new SpecApi { Method = "POST", Route = $"{route}/delete/{{ids}}", Action = $"Delete{name}", Permission = $"{prefix}:delete" });
            }
            if (replaceDto.ShowBtnExport)
            {
                apis.Add(new SpecApi { Method = "GET", Route = $"{route}/export", Action = "Export", Permission = $"{prefix}:export" });
            }
            if (replaceDto.ShowBtnTruncate)
            {
                apis.Add(new SpecApi { Method = "POST", Route = $"{route}/clean", Action = "Clear", Permission = $"{prefix}:delete" });
            }
            if (replaceDto.ShowBtnImport)
            {
                apis.Add(new SpecApi { Method = "POST", Route = $"{route}/importData", Action = "ImportData", Permission = $"{prefix}:import" });
                apis.Add(new SpecApi { Method = "GET", Route = $"{route}/importTemplate", Action = "ImportTemplateExcel", Permission = "匿名" });
            }
            if (columns.Any(f => GenConstants.HTML_CUSTOM_INPUT.Equals(f.HtmlType, StringComparison.OrdinalIgnoreCase)))
            {
                apis.Add(new SpecApi { Method = "GET", Route = $"{route}/ChangeSort", Action = "ChangeSort", Permission = $"{prefix}:edit" });
            }

            return apis;
        }

        /// <summary>
        /// 权限码清单，从接口契约反推，避免与按钮勾选不一致
        /// </summary>
        private static List<SpecPermission> BuildPermissions(GenTable genTable, ReplaceDto replaceDto, List<SpecApi> apis)
        {
            var list = new List<SpecPermission>
            {
                new() { Code = $"{replaceDto.PermissionPrefix}:list", Name = $"{genTable.FunctionName}菜单（列表）" }
            };

            foreach (var api in apis)
            {
                if (string.IsNullOrEmpty(api.Permission) || api.Permission == "匿名") continue;
                if (list.Any(f => f.Code == api.Permission)) continue;
                list.Add(new SpecPermission { Code = api.Permission, Name = api.Action });
            }

            return list;
        }

        /// <summary>
        /// 项目约定，来自现有模板与 AGENTS.md 的硬性要求
        /// </summary>
        private static List<string> BuildConventions()
        {
            return new List<string>
            {
                "实体继承项目基类并标注 [SugarTable(\"表名\")]，主键/自增/列名不一致时用 [SugarColumn] 显式声明；主键为自增时不要生成赋值逻辑。",
                "Dto 放在实体同级的 Dto 目录，新增/修改共用 XxxDto，列表查询用 XxxQueryDto；不要为每个接口单独造 DTO。",
                "服务接口按模块分组在 I{模块}Service 目录，服务实现继承 BaseService<实体> 并实现接口，查询一律用 SqlSugar Queryable，禁止在循环里查库。",
                "控制器继承 BaseController，路由为 {模块}/{业务名}，每个接口都必须挂 ActionPermissionFilter 且权限码与本契约一致，写操作挂 [Log]。",
                "列表接口用数据库分页并返回 PagedInfo，禁止 ToList 后内存分页；查询条件按字段的“查询方式”构造（LIKE 用 Contains、BETWEEN 用区间）。",
                "删除接口按软删除开关处理：启用软删除时走更新标记字段，否则物理删除；批量删除入参是逗号分隔的 id 字符串。",
                "前端统一用项目已封装的 request，不直接 axios；Vue3 页面用 <script setup> + Element Plus，API 文件路径与函数名按本契约。",
                "新增功能必须配套菜单与按钮权限，权限码命名沿用 {前缀}:list/query/add/edit/delete/export/import，不另建体系。",
                "多租户场景下不得凭前端传入的 ID 直接跨租户访问，数据查询需带租户/权限范围限制。"
            };
        }

        #endregion

        #region 帮助方法

        private static string GetDbTypeName(int dbType)
        {
            return dbType switch
            {
                0 => "MySQL",
                1 => "SQL Server",
                2 => "SQLite",
                3 => "Oracle",
                4 => "PostgreSQL",
                _ => $"DbType={dbType}"
            };
        }

        private static string GetFrontTplText(int frontTpl)
        {
            return frontTpl switch
            {
                0 => "Ant Design Vue",
                1 => "Vue2 + Element UI",
                _ => "Vue3 + Element Plus"
            };
        }

        private static string GetTplCategoryText(string tplCategory)
        {
            return tplCategory switch
            {
                "tree" => "树表",
                "sub" => "主子表",
                "subNav" => "主子表（一对一导航）",
                "subNavMore" => "主子表（一对多导航）",
                _ => "单表 CRUD"
            };
        }

        /// <summary>
        /// 表格单元格转义，避免中文名里的竖线/换行破坏 Markdown 表格
        /// </summary>
        private static string Cell(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }

        private static string Mark(bool value)
        {
            return value ? "✓" : "";
        }

        #endregion
    }
}
