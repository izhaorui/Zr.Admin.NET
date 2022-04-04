using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Model;
using Mapster;
using ZR.Model.Dto;
using ZR.Model.Models;
using ZR.Service.Business.IBusinessService;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.Common;
using Infrastructure.Extensions;
using System.Linq;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 演示Controller
    ///
    /// @author zz
    /// @date 2022-03-31
    /// </summary>
    [Verify]
    [Route("business/GenDemo")]
    public class GenDemoController : BaseController
    {
        /// <summary>
        /// 演示接口
        /// </summary>
        private readonly IGenDemoService _GenDemoService;

        public GenDemoController(IGenDemoService GenDemoService)
        {
            _GenDemoService = GenDemoService;
        }

        /// <summary>
        /// 查询演示列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "business:gendemo:list")]
        public IActionResult QueryGenDemo([FromQuery] GenDemoQueryDto parm)
        {
            var response = _GenDemoService.GetList(parm);
            return SUCCESS(response);
        }


        /// <summary>
        /// 查询演示详情
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [HttpGet("{Id}")]
        [ActionPermissionFilter(Permission = "business:gendemo:query")]
        public IActionResult GetGenDemo(int Id)
        {
            var response = _GenDemoService.GetFirst(x => x.Id == Id);
            
            return SUCCESS(response);
        }

        /// <summary>
        /// 添加演示
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ActionPermissionFilter(Permission = "business:gendemo:add")]
        [Log(Title = "演示", BusinessType = BusinessType.INSERT)]
        public IActionResult AddGenDemo([FromBody] GenDemoDto parm)
        {
            if (parm == null)
            {
                throw new CustomException("请求参数错误");
            }
            //从 Dto 映射到 实体
            var modal = parm.Adapt<GenDemo>().ToCreate(HttpContext);

            var response = _GenDemoService.Insert(modal, it => new
            {
                it.Name,
                it.Icon,
                it.ShowStatus,
                it.Sex,
                it.Sort,
                it.Remark,
                it.BeginTime,
                it.EndTime,
                it.Feature,
            });
            return ToResponse(response);
        }

        /// <summary>
        /// 更新演示
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [ActionPermissionFilter(Permission = "business:gendemo:edit")]
        [Log(Title = "演示", BusinessType = BusinessType.UPDATE)]
        public IActionResult UpdateGenDemo([FromBody] GenDemoDto parm)
        {
            if (parm == null)
            {
                throw new CustomException("请求实体不能为空");
            }
            //从 Dto 映射到 实体
            var modal = parm.Adapt<GenDemo>().ToUpdate(HttpContext);

            var response = _GenDemoService.Update(w => w.Id == modal.Id, it => new GenDemo()
            {
                //Update 字段映射
                Name = modal.Name,
                Icon = modal.Icon,
                ShowStatus = modal.ShowStatus,
                Sex = modal.Sex,
                Sort = modal.Sort,
                Remark = modal.Remark,
                BeginTime = modal.BeginTime,
                EndTime = modal.EndTime,
                Feature = modal.Feature,
            });

            return ToResponse(response);
        }

        /// <summary>
        /// 删除演示
        /// </summary>
        /// <returns></returns>
        [HttpDelete("{ids}")]
        [ActionPermissionFilter(Permission = "business:gendemo:delete")]
        [Log(Title = "演示", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteGenDemo(string ids)
        {
            int[] idsArr = Tools.SpitIntArrary(ids);
            if (idsArr.Length <= 0) { return ToResponse(ApiResult.Error($"删除失败Id 不能为空")); }

            var response = _GenDemoService.Delete(idsArr);

            return ToResponse(response);
        }

        /// <summary>
        /// 导出演示
        /// </summary>
        /// <returns></returns>
        [Log(Title = "演示", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "business:gendemo:export")]
        public IActionResult Export([FromQuery] GenDemoQueryDto parm)
        {
            parm.PageSize = 10000;
            var list = _GenDemoService.GetList(parm).Result;

            string sFileName = ExportExcel(list, "GenDemo", "演示");
            return SUCCESS(new { path = "/export/" + sFileName, fileName = sFileName });
        }

    }
}