using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System;
using System.Collections.Generic;
using ZR.Admin.WebApi.Filters;
using ZR.CodeGenerator;
using ZR.CodeGenerator.CodeGenerator;
using ZR.CodeGenerator.Model;
using ZR.CodeGenerator.Service;
using ZR.Common;
using ZR.Model;
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
        /// 获取表格列
        /// </summary>
        /// <param name="dbName"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        //[HttpGet("getColumnInfo")]
        //[ActionPermissionFilter(Permission = "tool:gen:list")]
        //public IActionResult QueryColumnInfo(string dbName, string tableName)
        //{
        //    if (string.IsNullOrEmpty(dbName) || string.IsNullOrEmpty(tableName))
        //        return ToRespose(ResultCode.PARAM_ERROR);

        //    return SUCCESS(_CodeGeneraterService.GetColumnInfo(dbName, tableName));
        //}

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
            if (string.IsNullOrEmpty(dto.tableName))
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "请求参数为空");
            }
            DbTableInfo dbTableInfo = new() { Name = dto.tableName };
            CodeGeneratorTool.Generate(dbTableInfo, dto);

            return SUCCESS(dbTableInfo);
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
        /// 代码生成删除
        /// </summary>
        /// <param name="tableIds"></param>
        /// <returns></returns>
        [Log(Title = "代码生成", BusinessType = BusinessType.DELETE)]
        [HttpDelete("{tableIds}")]
        [ActionPermissionFilter(Permission = "tool:gen:remove")]
        public IActionResult Remove(string tableIds)
        {
            long[] tableId = Tools.SpitLongArrary(tableIds);
            //TODO 带做 删除表
            return SUCCESS("");
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

            foreach (var item in tableNames)
            {
                var tabInfo = _CodeGeneraterService.GetTableInfo(dbName, item);
                if (tabInfo != null)
                {
                    GenTable genTable = new()
                    {
                        TableName = item,
                        TableComment = tabInfo.Description,
                        ClassName = CodeGeneratorTool.GetModelClassName(item),
                        CreateBy = userName,
                        CreateTime = DateTime.Now
                    };
                    int rows = GenTableService.InsertGenTable(genTable);
                    if (rows > 0)
                    {
                        //保存列信息
                        List<DbColumnInfo> dbColumnInfos = _CodeGeneraterService.GetColumnInfo(dbName, item);
                        List<GenTableColumn> genTableColumns = new();
                        long tableId = 0;
                        foreach (var column in dbColumnInfos)
                        {
                            tableId = column.TableId;

                            GenTableColumn genTableColumn = new()
                            {
                                ColumnName = column.DbColumnName,
                                ColumnComment = column.ColumnDescription,
                                IsPk = column.IsPrimarykey,
                                ColumnType = column.DataType,
                                TableId = rows,
                                TableName = item,
                                CsharpType = TableMappingHelper.GetPropertyDatatype(column.DataType),
                                CsharpField = column.DbColumnName.Substring(0, 1).ToUpper() + column.DbColumnName[1..],
                                IsRequired = column.IsNullable,
                                IsIncrement = column.IsIdentity,
                                CreateBy = userName,
                                CreateTime = DateTime.Now,
                                IsInsert = true,
                                IsEdit = true,
                                IsList = true,
                                IsQuery = false
                            };
                            genTableColumns.Add(genTableColumn);
                        }

                        GenTableColumnService.DeleteGenTableColumn(tableId);
                        GenTableColumnService.InsertGenTableColumn(genTableColumns);
                    }
                }
            }

            return SUCCESS(1);
        }
    }
}
