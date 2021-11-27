using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SqlSugar;
using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Model;
using Mapster;
using ZR.Model.Dto;
using ZR.Model.Models;
using ZR.Service.Business;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.Common;
using Infrastructure.Extensions;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 代码生成演示Controller
    ///
    /// @author zr
    /// @date 2021-11-24
    /// </summary>
    [Verify]
    [Route("business/Gendemo")]
    public class GendemoController : BaseController
    {
        /// <summary>
        /// 代码生成演示接口
        /// </summary>
        private readonly IGendemoService _GendemoService;

        public GendemoController(IGendemoService GendemoService)
        {
            _GendemoService = GendemoService;
        }

        /// <summary>
        /// 查询代码生成演示列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "business:gendemo:list")]
        public IActionResult QueryGendemo([FromQuery] GendemoQueryDto parm)
        {
            //开始拼装查询条件
            var predicate = Expressionable.Create<Gendemo>();

            //TODO 自己实现搜索条件查询语法参考Sqlsugar，默认查询所有
            //predicate = predicate.And(m => m.Name.Contains(parm.Name));

            var response = _GendemoService.GetPages(predicate.ToExpression(), parm);

            return SUCCESS(response);
        }

        /// <summary>
        /// 查询代码生成演示详情
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [HttpGet("{Id}")]
        [ActionPermissionFilter(Permission = "business:gendemo:query")]
        public IActionResult GetGendemo(int Id)
        {
            var response = _GendemoService.GetId(Id);

            return SUCCESS(response);
        }

        /// <summary>
        /// 添加代码生成演示
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ActionPermissionFilter(Permission = "business:gendemo:add")]
        [Log(Title = "代码生成演示", BusinessType = BusinessType.INSERT)]
        public IActionResult AddGendemo([FromBody] GendemoDto parm)
        {
            if (parm == null)
            {
                throw new CustomException("请求参数错误");
            }
            //从 Dto 映射到 实体
            var model = parm.Adapt<Gendemo>().ToCreate(HttpContext);

            return SUCCESS(_GendemoService.Insert(model, it => new
            {
                it.Name,
                it.Icon,
                it.ShowStatus,
                it.AddTime,
                it.Sex,
                it.Sort,
                it.BeginTime,
                it.EndTime,
                it.Remark,
            }));
        }

        /// <summary>
        /// 更新代码生成演示
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [ActionPermissionFilter(Permission = "business:gendemo:update")]
        [Log(Title = "代码生成演示", BusinessType = BusinessType.UPDATE)]
        public IActionResult UpdateGendemo([FromBody] GendemoDto parm)
        {
            if (parm == null)
            {
                throw new CustomException("请求实体不能为空");
            }
            //从 Dto 映射到 实体
            var model = parm.Adapt<Gendemo>().ToUpdate(HttpContext);

            var response = _GendemoService.Update(w => w.Id == model.Id, it => new Gendemo()
            {
                //Update 字段映射
                Name = model.Name,
                Icon = model.Icon,
                ShowStatus = model.ShowStatus,
                AddTime = model.AddTime,
                Sex = model.Sex,
                Sort = model.Sort,
                BeginTime = model.BeginTime,
                EndTime = model.EndTime,
                Remark = model.Remark,
            });

            return SUCCESS(response);
        }

        /// <summary>
        /// 删除代码生成演示
        /// </summary>
        /// <returns></returns>
        [HttpDelete("{ids}")]
        [ActionPermissionFilter(Permission = "business:gendemo:delete")]
        [Log(Title = "代码生成演示", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteGendemo(string ids)
        {
            int[] idsArr = Tools.SpitIntArrary(ids);
            if (idsArr.Length <= 0) { return ToResponse(ApiResult.Error($"删除失败Id 不能为空")); }

            var response = _GendemoService.Delete(idsArr);

            return SUCCESS(response);
        }
    }
}