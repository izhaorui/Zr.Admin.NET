using Infrastructure.Attribute;
using Infrastructure.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Service.System.IService;

namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 数据字典信息
    /// </summary>
    [Verify]
    [Route("system/dict/data")]
    public class SysDictDataController : BaseController
    {
        private readonly ISysDictDataService SysDictDataService;
        private readonly ISysDictService SysDictService;

        public SysDictDataController(ISysDictService sysDictService, ISysDictDataService sysDictDataService)
        {
            SysDictService = sysDictService;
            SysDictDataService = sysDictDataService;
        }

        /// <summary>
        /// 搜索
        /// </summary>
        /// <param name="dictData"></param>
        /// <param name="pagerInfo"></param>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "system:dict:list")]
        [HttpGet("list")]
        public IActionResult List([FromQuery] SysDictData dictData, [FromQuery] PagerInfo pagerInfo)
        {
            var list = SysDictDataService.SelectDictDataList(dictData, pagerInfo);
            return SUCCESS(list);
        }

        /// <summary>
        /// 根据字典类型查询字典数据信息
        /// </summary>
        /// <param name="dictType"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet("type/{dictType}")]
        public IActionResult DictType(string dictType)
        {
            return SUCCESS(SysDictDataService.SelectDictDataByType(dictType));
        }

        /// <summary>
        /// 根据字典类型查询字典数据信息
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpPost("types")]
        public IActionResult DictTypes([FromBody] List<SysdictDataDto> dto)
        {
            var list = SysDictDataService.SelectDictDataByTypes(dto.Select(f => f.DictType).ToArray());
            List<SysdictDataDto> dataVos = new();

            foreach (var dic in dto)
            {
                SysdictDataDto vo = new()
                {
                    DictType = dic.DictType,
                    ColumnName = dic.ColumnName,
                    List = list.FindAll(f => f.DictType == dic.DictType)
                };
                dataVos.Add(vo);
            }
            return SUCCESS(dataVos);
        }

        /// <summary>
        /// 查询字典数据详细
        /// </summary>
        /// <param name="dictCode"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet("info/{dictCode}")]
        public IActionResult GetInfo(long dictCode)
        {
            return SUCCESS(SysDictDataService.SelectDictDataById(dictCode));
        }

        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "system:dict:add")]
        [Log(Title = "字典数据", BusinessType = BusinessType.INSERT)]
        [HttpPost()]
        public IActionResult Add([FromBody] SysDictData dict)
        {
            dict.Create_by = HttpContext.GetName();
            dict.Create_time = DateTime.Now;
            return SUCCESS(SysDictDataService.InsertDictData(dict));
        }

        /// <summary>
        /// 修改
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "system:dict:edit")]
        [Log(Title = "字典数据", BusinessType = BusinessType.UPDATE)]
        [HttpPut()]
        public IActionResult Edit([FromBody] SysDictData dict)
        {
            dict.Update_by = HttpContext.GetName();
            return SUCCESS(SysDictDataService.UpdateDictData(dict));
        }

        /// <summary>
        /// 删除字典类型
        /// </summary>
        /// <param name="dictCode"></param>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "system:dict:remove")]
        [Log(Title = "字典类型", BusinessType = BusinessType.DELETE)]
        [HttpDelete("{dictCode}")]
        public IActionResult Remove(string dictCode)
        {
            long[] dictCodes = Common.Tools.SpitLongArrary(dictCode);

            return SUCCESS(SysDictDataService.DeleteDictDataByIds(dictCodes));
        }
    }
}
