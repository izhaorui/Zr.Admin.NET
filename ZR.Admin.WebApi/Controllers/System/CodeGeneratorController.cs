﻿using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Mapster;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.CodeGenerator;
using ZR.CodeGenerator.Model;
using ZR.CodeGenerator.Service;
using ZR.Common;
using ZR.Model;
using ZR.Model.System.Dto;
using ZR.Model.System.Generate;
using ZR.Service.System.IService;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 代码生成
    /// </summary>
    [Verify]
    [Route("tool/gen")]
    public class CodeGeneratorController : BaseController
    {
        private readonly CodeGeneraterService _CodeGeneraterService = new CodeGeneraterService();
        private readonly IGenTableService GenTableService;
        private readonly IGenTableColumnService GenTableColumnService;

        private readonly IWebHostEnvironment WebHostEnvironment;
        public CodeGeneratorController(
            IGenTableService genTableService,
            IGenTableColumnService genTableColumnService,
            IWebHostEnvironment webHostEnvironment)
        {
            GenTableService = genTableService;
            GenTableColumnService = genTableColumnService;
            WebHostEnvironment = webHostEnvironment;
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
        ///获取所有表根据数据库名
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

            return SUCCESS(list.ToPage(pager));
        }

        /// <summary>
        /// 查询生成表数据
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="pagerInfo">分页信息</param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "tool:gen:list")]
        public IActionResult GetGenTable(string tableName, PagerInfo pagerInfo)
        {
            //查询原表数据，部分字段映射到代码生成表字段
            var rows = GenTableService.GetGenTables(new GenTable() { TableName = tableName }, pagerInfo);

            return SUCCESS(rows);
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
            var tableColumns = GenTableColumnService.GenTableColumns(tableId);
            var tableInfo = GenTableService.GetGenTableInfo(tableId);
            var tables = GenTableService.GetGenTableAll();

            return SUCCESS(new { columns = tableColumns, info = tableInfo, tables });
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
                    GenTable genTable = CodeGeneratorTool.InitTable(dbName, userName, tableName, tabInfo?.Description);
                    genTable.TableId = GenTableService.ImportGenTable(genTable);

                    if (genTable.TableId > 0)
                    {
                        //保存列信息
                        List<DbColumnInfo> dbColumnInfos = _CodeGeneraterService.GetColumnInfo(dbName, tableName);
                        List<GenTableColumn> genTableColumns = CodeGeneratorTool.InitGenTableColumn(genTable, dbColumnInfos);

                        GenTableColumnService.DeleteGenTableColumnByTableName(tableName);
                        GenTableColumnService.InsertGenTableColumn(genTableColumns);
                        genTable.Columns = genTableColumns;

                        return SUCCESS(genTable);
                    }
                }
            }

            return ToResponse(ResultCode.FAIL);
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
            var genTable = genTableDto.Adapt<GenTable>().ToUpdate(HttpContext);

            genTable.Options = JsonConvert.SerializeObject(new
            {
                parentMenuId = genTableDto.ParentMenuId,
                sortField = genTableDto.SortField,
                sortType = genTable.SortType
            });
            int updateCount = 0;
            bool result = GenTableService.UseTran2(() =>
            {
                int rows = GenTableService.UpdateGenTable(genTable);
                if (rows > 0)
                {
                    updateCount = GenTableColumnService.UpdateGenTableColumn(genTable.Columns);
                }
            });

            return SUCCESS(updateCount);
        }

        /// <summary>
        /// 预览代码
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("preview/{tableId}")]
        [ActionPermissionFilter(Permission = "tool:gen:preview")]
        public IActionResult Preview([FromBody] GenerateDto dto)
        {
            if (dto == null || dto.TableId <= 0)
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "请求参数为空");
            }
            var genTableInfo = GenTableService.GetGenTableInfo(dto.TableId);
            genTableInfo.Columns = GenTableColumnService.GenTableColumns(dto.TableId);

            dto.GenTable = genTableInfo;
            dto.ZipPath = Path.Combine(WebHostEnvironment.WebRootPath, "Generatecode");
            dto.GenCodePath = Path.Combine(dto.ZipPath, DateTime.Now.ToString("yyyyMMdd"));
            dto.IsPreview = 1;
            //生成代码
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
            if (dto.TableId <= 0)
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "请求参数为空");
            }
            dto.ZipPath = Path.Combine(WebHostEnvironment.WebRootPath, "Generatecode");
            dto.GenCodePath = Path.Combine(dto.ZipPath, DateTime.Now.ToString("yyyyMMdd"));

            var genTableInfo = GenTableService.GetGenTableInfo(dto.TableId);
            genTableInfo.Columns = GenTableColumnService.GenTableColumns(dto.TableId);

            dto.GenTable = genTableInfo;
            //生成代码
            CodeGeneratorTool.Generate(dto);
            //下载文件
            FileHelper.ZipGenCode(dto);

            return SUCCESS(new { path = "/Generatecode/" + dto.ZipFileName, fileName = dto.ZipFileName });
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
            if (table == null) { throw new CustomException("原表不存在"); }

            List<DbColumnInfo> dbColumnInfos = _CodeGeneraterService.GetColumnInfo(table.DbName, tableName);
            List<GenTableColumn> dbTableColumns = CodeGeneratorTool.InitGenTableColumn(table, dbColumnInfos);

            GenTableService.SynchDb(tableId, table, dbTableColumns);
            return SUCCESS(true);
        }
    }
}
