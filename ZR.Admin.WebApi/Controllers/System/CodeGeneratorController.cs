using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SqlSugar;
using ZR.CodeGenerator;
using ZR.CodeGenerator.Model;
using ZR.CodeGenerator.Service;
using ZR.Model;
using ZR.Model.System.Dto;
using ZR.Model.System.Generate;
using ZR.ServiceCore.AI.IService;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 代码生成
    /// </summary>
    [Route("tool/gen")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class CodeGeneratorController : BaseController
    {
        private readonly CodeGeneraterService _CodeGeneraterService = new CodeGeneraterService();
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly IGenTableService GenTableService;
        private readonly IGenTableColumnService GenTableColumnService;
        private readonly ISysMenuService SysMenuService;
        private readonly IWebHostEnvironment WebHostEnvironment;
        private readonly OptionsSetting OptionsSetting;
        private readonly ISysAiService SysAiService;

        public CodeGeneratorController(
            IGenTableService genTableService,
            IGenTableColumnService genTableColumnService,
            IWebHostEnvironment webHostEnvironment,
            IOptions<OptionsSetting> options,
            ISysMenuService sysMenuService,
            ISysAiService sysAiService)
        {
            GenTableService = genTableService;
            GenTableColumnService = genTableColumnService;
            WebHostEnvironment = webHostEnvironment;
            SysMenuService = sysMenuService;
            OptionsSetting = options.Value;
            SysAiService = sysAiService;
        }

        /// <summary>
        /// AI 推断列配置建议（中文标签/控件类型/是否列表查询等）。
        /// 只返回建议不写库，采纳与否由用户在列配置页勾选后走既有保存接口。
        /// </summary>
        [HttpPost("ai/suggestColumns/{tableId}")]
        [ActionPermissionFilter(Permission = "tool:gen:ai")]
        public async Task<IActionResult> AiSuggestColumns([FromRoute] long tableId)
        {
            try
            {
                return SUCCESS(await SysAiService.SuggestGenColumnsAsync(tableId));
            }
            catch (Exception ex)
            {
                return ToResponse(ResultCode.FAIL, ex.Message);
            }
        }

        /// <summary>
        /// AI 推断表级配置建议（模板类型/模块名/业务名/权限前缀/操作日志/按钮）。
        /// 只返回建议不写库，采纳与否由用户在配置页勾选后走 ai/apply/{tableId}。
        /// </summary>
        [HttpPost("ai/suggestTable/{tableId}")]
        [ActionPermissionFilter(Permission = "tool:gen:ai")]
        public async Task<IActionResult> AiSuggestTable([FromRoute] long tableId)
        {
            try
            {
                return SUCCESS(await SysAiService.SuggestGenTableAsync(tableId));
            }
            catch (Exception ex)
            {
                return ToResponse(ResultCode.FAIL, ex.Message);
            }
        }

        /// <summary>
        /// 采纳 AI 建议写回代码生成配置（表级字段 + 列配置）。
        /// 服务端重新校验全部取值，表与列在同一事务内提交。
        /// </summary>
        [HttpPost("ai/apply/{tableId}")]
        [ActionPermissionFilter(Permission = "tool:gen:edit")]
        [Log(Title = "代码生成", BusinessType = BusinessType.UPDATE, IsSaveRequestData = false)]
        public IActionResult AiApply([FromRoute] long tableId, [FromBody] SysAiGenApplyInput dto)
        {
            try
            {
                return SUCCESS(SysAiService.ApplyGenSuggestion(tableId, dto, HttpContext.GetName()));
            }
            catch (Exception ex)
            {
                return ToResponse(ResultCode.FAIL, ex.Message);
            }
        }

        /// <summary>
        /// 获取所有数据库的信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("getDbList")]
        [ActionPermissionFilter(Permission = "tool:gen:list")]
        public IActionResult GetListDataBase()
        {
            var dbList = _CodeGeneraterService.GetAllDataBases();
            var defaultDb = dbList?.FirstOrDefault();
            return SUCCESS(new { dbList, defaultDb });
        }

        /// <summary>
        ///获取所有表根据数据库名
        /// </summary>
        /// <param name="dbName">数据库名</param>
        /// <param name="tableName">表名</param>
        /// <param name="pager">分页信息</param>
        /// <returns></returns>
        [HttpGet("getTableList")]
        [ActionPermissionFilter(Permission = "tool:gen:list")]
        public IActionResult FindListTable(string dbName, string? tableName, PagerInfo pager)
        {
            List<DbTableInfo> list = _CodeGeneraterService.GetAllTables(dbName, tableName, pager);
            var page = new PagedInfo<DbTableInfo>
            {
                TotalNum = pager.TotalNum,
                PageSize = pager.PageSize,
                PageIndex = pager.PageNum,
                Result = list
            };
            return SUCCESS(page);
        }

        /// <summary>
        /// 查询生成表数据
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="pagerInfo">分页信息</param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "tool:gen:list")]
        public IActionResult GetGenTable(string? tableName, string? tableComment, string? tplCategory, string? dbName, PagerInfo pagerInfo)
        {
            //查询原表数据，部分字段映射到代码生成表字段
            var rows = GenTableService.GetGenTables(new GenTable() { TableName = tableName, TableComment = tableComment, TplCategory = tplCategory, DbName = dbName }, pagerInfo);

            return SUCCESS(rows, "MM月dd日 HH:mm");
        }

        /// <summary>
        /// 修改代码生成业务查询
        /// </summary>
        /// <param name="tableId">genTable表id</param>
        /// <returns></returns>
        [HttpGet("{tableId}")]
        [ActionPermissionFilter(Permission = "tool:gen:query")]
        public IActionResult GetColumnList(long tableId)
        {
            var tableInfo = EnsureAndGetGenTable(tableId);
            var tables = GenTableService.GetGenTableAll();
            return SUCCESS(new { info = tableInfo, tables });
        }

        /// <summary>
        /// 根据表id查询表列
        /// </summary>
        /// <param name="tableId">genTable表id</param>
        /// <returns></returns>
        [HttpGet("column/{tableId}")]
        [ActionPermissionFilter(Permission = "tool:gen:query")]
        public IActionResult GetTableColumnList(long tableId)
        {
            var tableColumns = GenTableColumnService.GenTableColumns(tableId);

            return SUCCESS(new { columns = tableColumns });
        }

        /// <summary>
        /// 确保子表已导入代码生成（未导入时按物理表结构自动导入），返回是否发生了导入
        /// </summary>
        /// <param name="genTable">主表信息</param>
        private bool EnsureSubTablesImported(GenTable genTable)
        {
            if (genTable == null) return false;

            var subNames = new List<(string Name, string FkName)>();
            var configs = genTable.Options?.SubTables;
            if (configs != null)
            {
                subNames.AddRange(configs
                    .Where(c => !string.IsNullOrEmpty(c?.TableName))
                    .Select(c => (c.TableName, c.FkName)));
            }
            if (!string.IsNullOrEmpty(genTable.SubTableName))
            {
                subNames.Add((genTable.SubTableName, genTable.SubTableFkName));
            }

            bool imported = false;
            foreach (var (name, _) in subNames.Distinct())
            {
                // 主表自己不可能做子表
                if (name.Equals(genTable.TableName, StringComparison.OrdinalIgnoreCase)) continue;
                // 已导入则跳过
                if (GenTableService.GetGenTableByName(name) != null) continue;
                // 物理库中不存在该表时给出明确错误，避免静默丢失
                var dbTable = _CodeGeneraterService.GetAllTables(genTable.DbName, name, new PagerInfo { PageNum = 1, PageSize = 10 })
                    .FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (dbTable == null)
                {
                    throw new CustomException($"子表 {name} 既未导入代码生成，数据库中也不存在，请检查配置");
                }
                ImportSingleTable(dbTable.Name, dbTable.Description, genTable.DbName);
                imported = true;
            }
            return imported;
        }

        /// <summary>
        /// 按物理表结构导入单张表到代码生成（与导入接口 ImportTableSave 相同逻辑）
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="description">表描述</param>
        /// <param name="dbName">数据库名</param>
        private void ImportSingleTable(string tableName, string description, string dbName)
        {
            if (string.IsNullOrEmpty(dbName))
            {
                dbName = _CodeGeneraterService.GetAllDataBases()?.FirstOrDefault();
            }
            if (string.IsNullOrEmpty(dbName))
            {
                throw new CustomException($"无法确定表 {tableName} 所在数据库，请先在导入页面手动导入");
            }

            InitTableDto initTableDto = new()
            {
                DbName = dbName,
                UserName = HttpContext.GetName(),
                TableName = tableName,
                Desc = description,
                CodeGen = OptionsSetting.CodeGen,
                FrontTpl = 2
            };

            GenTable genTable = CodeGeneratorTool.InitTable(initTableDto);
            genTable.TableId = GenTableService.ImportGenTable(genTable);
            if (genTable.TableId > 0)
            {
                List<OracleSeq> seqs = new();
                if (OptionsSetting.CodeGenDbConfig.DbType == 3)
                {
                    seqs = _CodeGeneraterService.GetAllOracleSeqs();
                }
                List<DbColumnInfo> dbColumnInfos = _CodeGeneraterService.GetColumnInfo(dbName, tableName);
                List<GenTableColumn> genTableColumns = CodeGeneratorTool.InitGenTableColumn(genTable, dbColumnInfos, seqs, OptionsSetting.CodeGen);
                GenTableColumnService.DeleteGenTableColumnByTableName(tableName);
                GenTableColumnService.InsertGenTableColumn(genTableColumns);
            }
        }

        /// <summary>
        /// 获取主表信息，并自动导入配置了但尚未导入的子表（编辑/预览/生成共用）
        /// </summary>
        /// <param name="tableId">主表id</param>
        private GenTable EnsureAndGetGenTable(long tableId)
        {
            var info = GenTableService.GetGenTableInfo(tableId);
            if (info == null) return info;
            bool changed = EnsureSubTablesImported(info);
            // 发生了自动导入时重新加载，保证子表及列信息完整
            return changed ? GenTableService.GetGenTableInfo(tableId) : info;
        }

        /// <summary>
        /// 删除代码生成
        /// </summary>
        /// <param name="tableIds"></param>
        /// <returns></returns>
        [Log(Title = "代码生成", BusinessType = BusinessType.DELETE)]
        [HttpDelete("{tableIds}")]
        [ActionPermissionFilter(Permission = "tool:gen:remove")]
        public IActionResult Remove(string tableIds)
        {
            long[] tableId = Tools.SpitLongArrary(tableIds);

            int result = GenTableService.DeleteGenTableByIds(tableId);
            return SUCCESS(result);
        }

        /// <summary>
        /// 导入表结构（保存）
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("importTable")]
        [Log(Title = "代码生成", BusinessType = BusinessType.IMPORT)]
        [ActionPermissionFilter(Permission = "tool:gen:import")]
        public IActionResult ImportTableSave([FromBody] ImportCodeGenTableDto dto)
        {
            if (string.IsNullOrEmpty(dto.DbName) || dto.Tables == null)
            {
                throw new CustomException("表或数据库不能为空");
            }

            int result = 0;
            foreach (var table in dto.Tables)
            {
                List<OracleSeq> seqs = new();
                InitTableDto initTableDto = new()
                {
                    DbName = dto.DbName,
                    UserName = HttpContext.GetName(),
                    TableName = table.Name,
                    Desc = table.Description,
                    CodeGen = OptionsSetting.CodeGen,
                    FrontTpl = dto.FrontTpl,
                };

                GenTable genTable = CodeGeneratorTool.InitTable(initTableDto);
                genTable.TableId = GenTableService.ImportGenTable(genTable);
                if (OptionsSetting.CodeGenDbConfig.DbType == 3)
                {
                    seqs = _CodeGeneraterService.GetAllOracleSeqs();
                }
                if (genTable.TableId > 0)
                {
                    //保存列信息
                    List<DbColumnInfo> dbColumnInfos = _CodeGeneraterService.GetColumnInfo(dto.DbName, table.Name);
                    List<GenTableColumn> genTableColumns = CodeGeneratorTool.InitGenTableColumn(genTable, dbColumnInfos, seqs, OptionsSetting.CodeGen);
                    GenTableColumnService.DeleteGenTableColumnByTableName(table.Name);
                    GenTableColumnService.InsertGenTableColumn(genTableColumns);
                    genTable.Columns = genTableColumns;
                    result++;
                }
            }

            return ToResponse(result);
        }

        /// <summary>
        /// 修改保存代码生成业务
        /// </summary>
        /// <param name="genTableDto">请求参数实体</param>
        /// <returns></returns>
        [HttpPut]
        [Log(Title = "代码生成", BusinessType = BusinessType.GENCODE, IsSaveRequestData = false)]
        [ActionPermissionFilter(Permission = "tool:gen:edit")]
        public IActionResult EditSave([FromBody] GenTableDto genTableDto)
        {
            if (genTableDto == null) throw new CustomException("请求参数错误");
            if (genTableDto.BusinessName.Equals(genTableDto.ModuleName, StringComparison.OrdinalIgnoreCase))
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "模块名不能和业务名一样");
            }
            var genTable = genTableDto.Adapt<GenTable>().ToUpdate(HttpContext);

            //将前端额外参数转成字符串存入Options中
            genTable.Options = genTableDto.Params.Adapt<CodeOptions>();
            DbResult<bool> result = GenTableService.UseTran(() =>
            {
                int rows = GenTableService.UpdateGenTable(genTable);
                if (rows > 0)
                {
                    GenTableColumnService.UpdateGenTableColumn(genTable.Columns);
                }
            });
            if (!result.IsSuccess)
            {
                // 事务失败向上抛出真实原因，避免被吞掉后前端只看到 false
                throw result.ErrorException ?? new CustomException("保存代码生成配置失败：" + result.ErrorMessage);
            }

            return SUCCESS(result.IsSuccess);
        }

        /// <summary>
        /// 预览代码
        /// </summary>
        /// <param name="dto"></param>
        /// <param name="tableId"></param>
        /// <returns></returns>
        [HttpPost("preview/{tableId}")]
        [ActionPermissionFilter(Permission = "tool:gen:preview")]
        public IActionResult Preview([FromQuery] GenerateDto dto, [FromRoute] long tableId = 0)
        {
            if (dto == null || tableId <= 0)
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "请求参数为空");
            }
            dto.TableId = tableId;
            var genTableInfo = EnsureAndGetGenTable(dto.TableId);

            dto.DbType = OptionsSetting.CodeGenDbConfig.DbType;
            dto.GenTable = genTableInfo;
            dto.IsPreview = true;

            CodeGeneratorTool.Generate(dto);

            return SUCCESS(dto.GenCodes);
        }

        /// <summary>
        /// 导出 AI 契约：把当前表配置整理成结构化描述（字段语义、权限码、接口契约、目标文件等），
        /// 供 AI / 编码助手作为上下文使用。只读配置，不渲染模板、不写库、不影响原有生成流程。
        /// </summary>
        /// <param name="tableId">genTable表id</param>
        /// <returns>spec：结构化契约；markdown：可直接复制给 AI 的文本</returns>
        [HttpGet("spec/{tableId}")]
        [ActionPermissionFilter(Permission = "tool:gen:query")]
        public IActionResult GetSpec([FromRoute] long tableId)
        {
            if (tableId <= 0)
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "请求参数为空");
            }
            var genTable = EnsureAndGetGenTable(tableId);
            if (genTable == null)
            {
                throw new CustomException("代码生成配置不存在");
            }

            var spec = CodeGenSpecBuilder.Build(genTable, OptionsSetting.CodeGenDbConfig.DbType);
            return SUCCESS(new { spec, markdown = CodeGenSpecBuilder.ToMarkdown(spec) });
        }

        /// <summary>
        /// 生成代码（下载方式）
        /// </summary>
        /// <param name="dto">数据传输对象</param>
        /// <returns></returns>
        [HttpPost("genCode")]
        [Log(Title = "代码生成", BusinessType = BusinessType.GENCODE)]
        [ActionPermissionFilter(Permission = "tool:gen:code")]
        public IActionResult CodeGenerate([FromBody] GenerateDto dto)
        {
            if (dto == null || dto.TableId <= 0)
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "请求参数为空");
            }

            dto.DbType = OptionsSetting.CodeGenDbConfig.DbType;
            dto.GenTable = EnsureAndGetGenTable(dto.TableId);
            //生成压缩包
            string zipReturnFileName = $"ZrAdmin.NET-{dto.GenTable.TableName}-{DateTime.Now:MMddHHmmss}.zip";

            //生成代码到指定文件夹
            CodeGeneratorTool.Generate(dto);
            if (dto.GenTable.Options.GenerateMenu)
            {
                SysMenuService.AddSysMenu(dto.GenTable, dto.ReplaceDto.PermissionPrefix, dto.ReplaceDto.ShowBtnEdit, dto.ReplaceDto.ShowBtnExport, dto.ReplaceDto.ShowBtnImport);
            }
            if (dto.GenTable.GenType == "1")//自定义路径
            {
                var genPath = dto.GenTable.GenPath;
                string parentPath;
                string tempPath = WebHostEnvironment.ContentRootPath;

                if (ComputerHelper.IsUnix())
                {
                    parentPath = Path.Combine(WebHostEnvironment.WebRootPath, "Generatecode");
                }
                else
                {
                    parentPath = tempPath[..tempPath.LastIndexOf(@"\")];
                }
                //代码生成文件夹路径
                dto.GenCodePath = (genPath.IsEmpty() || genPath.Equals("/")) ? parentPath : genPath;

                // 安全校验：自定义路径必须位于允许的根目录内，防止路径穿越向服务器任意位置写文件
                if (!IsSubPathOf(dto.GenCodePath, parentPath))
                {
                    logger.Warn($"代码生成拒绝非法自定义路径：genPath={genPath}，允许根目录={parentPath}，tableId={dto.TableId}");
                    throw new CustomException($"自定义生成路径必须在允许的目录内：{parentPath}");
                }
            }
            else
            {
                dto.ZipPath = Path.Combine(WebHostEnvironment.WebRootPath, "Generatecode");
                dto.GenCodePath = Path.Combine(dto.ZipPath, DateTime.Now.ToString("yyyyMMdd"));
            }

            //写入文件
            foreach (var item in dto.GenCodes)
            {
                item.Path = Path.Combine(dto.GenCodePath, item.Path);
                FileUtil.WriteAndSave(item.Path, item.Content);
            }
            if (dto.GenTable.GenType != "1")
            {
                var fullPath = Path.Combine(WebHostEnvironment.WebRootPath, "Generatecode", zipReturnFileName);

                //压缩文件
                FileUtil.ZipGenCode(dto.ZipPath, dto.GenCodePath, zipReturnFileName);
                Thread.Sleep(1000);
            }
            return SUCCESS(new { path = "/Generatecode/" + zipReturnFileName, fileName = dto.ZipFileName });
        }

        /// <summary>
        /// 同步数据库
        /// </summary>
        /// <param name="tableId"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "tool:gen:edit")]
        [Log(Title = "代码生成", BusinessType = BusinessType.UPDATE)]
        [HttpGet("synchDb/{tableId}")]
        public IActionResult SynchDb(string tableName, long tableId = 0)
        {
            if (string.IsNullOrEmpty(tableName) || tableId <= 0) throw new CustomException("参数错误");
            GenTable table = GenTableService.GetGenTableInfo(tableId);
            if (table == null) { throw new CustomException("同步数据失败，原表结构不存在"); }
            table.Update_by = HttpContext.GetName();

            // Oracle 需查序列判定自增列，与导入行为保持一致
            List<OracleSeq> seqs = null;
            if (OptionsSetting.CodeGenDbConfig.DbType == 3)
            {
                seqs = _CodeGeneraterService.GetAllOracleSeqs();
            }
            List<DbColumnInfo> dbColumnInfos = _CodeGeneraterService.GetColumnInfo(table.DbName, tableName);
            List<GenTableColumn> dbTableColumns = CodeGeneratorTool.InitGenTableColumn(table, dbColumnInfos, seqs, OptionsSetting.CodeGen);

            bool result = GenTableService.SynchDb(tableId, table, dbTableColumns);
            return SUCCESS(result);
        }

        /// <summary>
        /// 判断 target 路径是否位于 root 目录内（含等于 root），用于拦截路径穿越。
        /// Windows 下忽略大小写，Unix 下大小写敏感。
        /// </summary>
        private static bool IsSubPathOf(string target, string root)
        {
            if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(root)) return false;
            try
            {
                var fullTarget = Path.GetFullPath(target);
                var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var comparison = ComputerHelper.IsUnix() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                if (!fullTarget.StartsWith(fullRoot, comparison)) return false;
                return fullTarget.Length == fullRoot.Length
                    || fullTarget[fullRoot.Length] == Path.DirectorySeparatorChar
                    || fullTarget[fullRoot.Length] == Path.AltDirectorySeparatorChar;
            }
            catch
            {
                return false;
            }
        }
    }
}
