using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SqlSugar;
using ZR.Admin.WebApi.Filters;
using ZR.CodeGenerator;
using ZR.CodeGenerator.Model;
using ZR.CodeGenerator.Service;
using ZR.Model;
using ZR.Model.System.Dto;
using ZR.Model.System.Generate;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 代码生成
    /// </summary>
    [Verify]
    [Route("tool/gen")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class CodeGeneratorController : BaseController
    {
        private readonly CodeGeneraterService _CodeGeneraterService = new CodeGeneraterService();
        private readonly IGenTableService GenTableService;
        private readonly IGenTableColumnService GenTableColumnService;
        private readonly ISysMenuService SysMenuService;
        private readonly IWebHostEnvironment WebHostEnvironment;
        private readonly OptionsSetting OptionsSetting;

        public CodeGeneratorController(
            IGenTableService genTableService,
            IGenTableColumnService genTableColumnService,
            IWebHostEnvironment webHostEnvironment,
            IOptions<OptionsSetting> options,
            ISysMenuService sysMenuService)
        {
            GenTableService = genTableService;
            GenTableColumnService = genTableColumnService;
            WebHostEnvironment = webHostEnvironment;
            SysMenuService = sysMenuService;
            OptionsSetting = options.Value;
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
            var defaultDb = dbList?[0];
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
        public IActionResult GetGenTable(string? tableName, PagerInfo pagerInfo)
        {
            //查询原表数据，部分字段映射到代码生成表字段
            var rows = GenTableService.GetGenTables(new GenTable() { TableName = tableName }, pagerInfo);

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
            var tableInfo = GenTableService.GetGenTableInfo(tableId);
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
                    seqs = _CodeGeneraterService.GetAllOracleSeqs(table.Name);
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
        public IActionResult Preview([FromQuery] GenerateDto dto, [FromRoute] int tableId = 0)
        {
            dto.TableId = tableId;
            if (dto == null || dto.TableId <= 0)
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "请求参数为空");
            }
            var genTableInfo = GenTableService.GetGenTableInfo(dto.TableId);

            dto.DbType = OptionsSetting.CodeGenDbConfig.DbType;
            dto.GenTable = genTableInfo;
            dto.IsPreview = true;

            CodeGeneratorTool.Generate(dto);

            return SUCCESS(dto.GenCodes);
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
            dto.GenTable = GenTableService.GetGenTableInfo(dto.TableId);
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
                Console.WriteLine("代码生成路径" + parentPath);
                //代码生成文件夹路径
                dto.GenCodePath = (genPath.IsEmpty() || genPath.Equals("/")) ? parentPath : genPath;
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
                //压缩文件
                FileUtil.ZipGenCode(dto.ZipPath, dto.GenCodePath, zipReturnFileName);
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

            var codeGen = AppSettings.Get<CodeGen>("codeGen");
            List<DbColumnInfo> dbColumnInfos = _CodeGeneraterService.GetColumnInfo(table.DbName, tableName);
            List<GenTableColumn> dbTableColumns = CodeGeneratorTool.InitGenTableColumn(table, dbColumnInfos, codeGen: codeGen);

            bool result = GenTableService.SynchDb(tableId, table, dbTableColumns);
            return SUCCESS(result);
        }
    }
}
