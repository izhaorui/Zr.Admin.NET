using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.CodeGenerator;
using ZR.CodeGenerator.CodeGenerator;
using ZR.CodeGenerator.Model;
using ZR.CodeGenerator.Service;
using ZR.Common;
using ZR.Model;
using ZR.Model.System.Dto;
using ZR.Model.System.Generate;
using ZR.Model.Vo;
using ZR.Service.System.IService;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 代码生成
    /// </summary>
    [Route("tool/gen")]
    public class CodeGeneratorController : BaseController
    {
        private CodeGeneraterService _CodeGeneraterService = new CodeGeneraterService();
        private IGenTableService GenTableService;
        private IGenTableColumnService GenTableColumnService;
        public CodeGeneratorController(IGenTableService genTableService, IGenTableColumnService genTableColumnService)
        {
            GenTableService = genTableService;
            GenTableColumnService = genTableColumnService;
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
            var defaultDb = dbList.Count > 0 ? dbList[0] : null;
            return SUCCESS(new { dbList, defaultDb });
        }

        /// <summary>
        ///获取所有表根据数据名
        /// </summary>
        /// <param name="dbName">数据库名</param>
        /// <param name="tableName">表名</param>
        /// <param name="pager">分页信息</param>
        /// <returns></returns>
        [HttpGet("getTableList")]
        [ActionPermissionFilter(Permission = "tool:gen:list")]
        public IActionResult FindListTable(string dbName, string tableName, PagerInfo pager)
        {
            List<DbTableInfo> list = _CodeGeneraterService.GetAllTables(dbName, tableName, pager);
            var vm = new VMPageResult<DbTableInfo>(list, pager);

            return SUCCESS(vm);
        }

        /// <summary>
        /// 代码生成器
        /// </summary>
        /// <param name="dto">数据传输对象</param>
        /// <returns></returns>
        [HttpPost("genCode")]
        [Log(Title = "代码生成", BusinessType = BusinessType.GENCODE)]
        [ActionPermissionFilter(Permission = "tool:gen:code")]
        public IActionResult Generate([FromBody] GenerateDto dto)
        {
            if (dto.TableId <= 0)
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "请求参数为空");
            }
            var genTableInfo = GenTableService.GetGenTableInfo(dto.TableId);
            var getTableColumn = GenTableColumnService.GenTableColumns(dto.TableId);
            genTableInfo.Columns = getTableColumn;
            //DbTableInfo dbTableInfo = new() { Name = dto.tableName };
            CodeGeneratorTool.Generate(genTableInfo, dto);

            return SUCCESS(genTableInfo);
        }

        /// <summary>
        /// 获取表详细信息
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="pagerInfo">分页信息</param>
        /// <returns></returns>
        [HttpGet("getGenTable")]
        public IActionResult GetGenTable(string tableName, PagerInfo pagerInfo)
        {
            //if (string.IsNullOrEmpty(tableName))
            //{
            //    throw new CustomException(ResultCode.CUSTOM_ERROR, "请求参数为空");
            //}
            //查询原表数据，部分字段映射到代码生成表字段
            var rows = GenTableService.GetGenTables(new GenTable() { TableName = tableName }, pagerInfo);

            return SUCCESS(rows);
        }

        /// <summary>
        /// 查询表字段列表
        /// </summary>
        /// <param name="tableId"></param>
        /// <returns></returns>
        [HttpGet("column/{tableId}")]
        public IActionResult GetColumnList(long tableId)
        {
            var tableColumns = GenTableColumnService.GenTableColumns(tableId);
            var tableInfo = GenTableService.GetGenTableInfo(tableId);
            return SUCCESS(new { result = tableColumns, info = tableInfo });
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

            GenTableService.DeleteGenTableByIds(tableId);
            return SUCCESS(1);
        }

        /// <summary>
        /// 导入表
        /// </summary>
        /// <param name="tables"></param>
        /// <param name="dbName"></param>
        /// <returns></returns>
        [HttpPost("importTable")]
        [Log(Title = "代码生成", BusinessType = BusinessType.IMPORT)]
        [ActionPermissionFilter(Permission = "tool:gen:import")]
        public IActionResult ImportTableSave(string tables, string dbName)
        {
            if (string.IsNullOrEmpty(tables))
            {
                throw new CustomException("表不能为空");
            }
            string[] tableNames = tables.Split(',', StringSplitOptions.RemoveEmptyEntries);
            string userName = User.Identity.Name;

            foreach (var tableName in tableNames)
            {
                var tabInfo = _CodeGeneraterService.GetTableInfo(dbName, tableName);
                if (tabInfo != null)
                {
                    GenTable genTable = new()
                    {
                        BaseNameSpace = "ZR.",
                        ModuleName = "bus",
                        ClassName = CodeGeneratorTool.GetClassName(tableName),
                        BusinessName = CodeGeneratorTool.GetClassName(tableName),
                        FunctionAuthor = ConfigUtils.Instance.GetConfig(GenConstants.Gen_author),
                        FunctionName = tabInfo.Description,
                        TableName = tableName,
                        TableComment = tabInfo.Description,
                        Create_by = userName,
                    };
                    int rows = GenTableService.ImportGenTable(genTable);
                    if (rows > 0)
                    {
                        //保存列信息
                        List<DbColumnInfo> dbColumnInfos = _CodeGeneraterService.GetColumnInfo(dbName, tableName);
                        List<GenTableColumn> genTableColumns = new();
                        foreach (var column in dbColumnInfos)
                        {
                            GenTableColumn genTableColumn = new()
                            {
                                ColumnName = CodeGeneratorTool.FirstLowerCase(column.DbColumnName),
                                ColumnComment = column.ColumnDescription,
                                IsPk = column.IsPrimarykey,
                                ColumnType = column.DataType,
                                TableId = rows,
                                TableName = tableName,
                                CsharpType = TableMappingHelper.GetPropertyDatatype(column.DataType),
                                CsharpField = column.DbColumnName.Substring(0, 1).ToUpper() + column.DbColumnName[1..],
                                IsRequired = !column.IsNullable,
                                IsIncrement = column.IsIdentity,
                                Create_by = userName,
                                Create_time = DateTime.Now,
                                IsInsert = !column.IsIdentity && !column.IsPrimarykey,
                                IsEdit = !column.IsIdentity && !column.IsPrimarykey,
                                IsList = true,
                                IsQuery = false,
                                HtmlType = GenConstants.HTML_INPUT
                            };

                            if (CodeGeneratorTool.imageFiled.Any(f => column.DbColumnName.ToLower().Contains(f.ToLower())))
                            {
                                genTableColumn.HtmlType = GenConstants.HTML_IMAGE_UPLOAD;
                            }
                            else if (genTableColumn.CsharpType.ToLower().Contains("datetime"))
                            {
                                genTableColumn.HtmlType = GenConstants.HTML_DATETIME;
                            }
                            else if (CodeGeneratorTool.radioFiled.Any(f => column.DbColumnName.Contains(f)))
                            {
                                genTableColumn.HtmlType = GenConstants.HTML_RADIO;
                            }
                            else if (CodeGeneratorTool.selectFiled.Any(f => column.DbColumnName.Contains(f)))
                            {
                                genTableColumn.HtmlType = GenConstants.HTML_SELECT;
                            }
                            else if (column.Length > 300)
                            {
                                genTableColumn.HtmlType = GenConstants.HTML_TEXTAREA;
                            }

                            genTableColumns.Add(genTableColumn);
                        }

                        GenTableColumnService.DeleteGenTableColumnByTableName(tableName);
                        GenTableColumnService.InsertGenTableColumn(genTableColumns);
                    }
                }
            }

            return SUCCESS(1);
        }

        /// <summary>
        /// 代码生成保存
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        //[Log(Title = "代码生成", BusinessType = BusinessType.UPDATE)]
        [ActionPermissionFilter(Permission = "tool:gen:edit")]
        public IActionResult EditSave([FromBody] GenTableDto genTableDto)
        {
            if (genTableDto == null) throw new CustomException("请求参数错误");
            var genTable = genTableDto.Adapt<GenTable>().ToUpdate(HttpContext);

            int rows = GenTableService.UpdateGenTable(genTable);
            if (rows > 0)
            {
                GenTableColumnService.UpdateGenTableColumn(genTable.Columns);
            }
            return SUCCESS(rows);
        }
    }
}
