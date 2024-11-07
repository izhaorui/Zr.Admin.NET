﻿using Microsoft.AspNetCore.Mvc;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;


namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 数据字典信息
    /// </summary>
    [Verify]
    [Route("system/dict/type")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SysDictTypeController(ISysDictService sysDictService) : BaseController
    {

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="pagerInfo"></param>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "system:dict:list")]
        [HttpGet("list")]
        public IActionResult List([FromQuery] SysDictType dict, [FromQuery] PagerInfo pagerInfo)
        {
            var list = sysDictService.SelectDictTypeList(dict, pagerInfo);

            return SUCCESS(list, TIME_FORMAT_FULL);
        }

        /// <summary>
        /// 查询字典类型详细
        /// </summary>
        /// <param name="dictId"></param>
        /// <returns></returns>
        [HttpGet("{dictId}")]
        [ActionPermissionFilter(Permission = "system:dict:query")]
        public IActionResult GetInfo(long dictId = 0)
        {
            return SUCCESS(sysDictService.GetInfo(dictId));
        }

        /// <summary>
        /// 添加字典类型
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "system:dict:add")]
        [Log(Title = "字典操作", BusinessType = BusinessType.INSERT)]
        [HttpPost("edit")]
        public IActionResult Add([FromBody] SysDictTypeDto dto)
        {
            SysDictType dict = dto.Adapt<SysDictType>();
            if (UserConstants.NOT_UNIQUE.Equals(sysDictService.CheckDictTypeUnique(dict)))
            {
                return ToResponse(ApiResult.Error($"新增字典'{dict.DictName}'失败，字典类型已存在"));
            }
            dict.Create_by = HttpContext.GetName();
            dict.Create_time = DateTime.Now;
            return SUCCESS(sysDictService.InsertDictType(dict));
        }

        /// <summary>
        /// 修改字典类型
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "system:dict:edit")]
        [Log(Title = "字典操作", BusinessType = BusinessType.UPDATE)]
        [Route("edit")]
        [HttpPut]
        public IActionResult Edit([FromBody] SysDictTypeDto dto)
        {
            SysDictType dict = dto.Adapt<SysDictType>();
            if (UserConstants.NOT_UNIQUE.Equals(sysDictService.CheckDictTypeUnique(dict)))
            {
                return ToResponse(ApiResult.Error($"修改字典'{dict.DictName}'失败，字典类型已存在"));
            }
            //设置添加人
            dict.Update_by = HttpContext.GetName();
            return SUCCESS(sysDictService.UpdateDictType(dict));
        }

        /// <summary>
        /// 删除字典类型
        /// </summary>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "system:dict:remove")]
        [Log(Title = "删除字典类型", BusinessType = BusinessType.DELETE)]
        [HttpDelete("{ids}")]
        public IActionResult Remove(string ids)
        {
            long[] idss = Tools.SpitLongArrary(ids);

            return SUCCESS(sysDictService.DeleteDictTypeByIds(idss));
        }

        /// <summary>
        /// 字典导出
        /// </summary>
        /// <returns></returns>
        [Log(BusinessType = BusinessType.EXPORT, IsSaveResponseData = false, Title = "字典导出")]
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "system:dict:export")]
        public IActionResult Export()
        {
            var list = sysDictService.GetAll();

            string sFileName = ExportExcel(list, "sysdictType", "字典");
            return SUCCESS(new { path = "/export/" + sFileName, fileName = sFileName });
        }
    }
}
