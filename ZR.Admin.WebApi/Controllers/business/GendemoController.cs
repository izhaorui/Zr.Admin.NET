using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZR.Admin.WebApi.Filters;
using ZR.Admin.WebApi.Controllers;
using ZR.Service.Business;
using SqlSugar;
using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Model;
using Mapster;
using ZR.Admin.WebApi.Extensions;
using ZR.Model.Dto;
using ZR.Model.Models;

namespace ZRAdmin.Controllers
{
    /// <summary>
    /// 代码自动生成
    /// </summary>

    //[Verify]
    [Route("bus/gendemo")]
    public class GendemoController : BaseController
    {
        /// <summary>
        /// 接口
        /// </summary>
        private readonly IGendemoService _GendemoService;

        public GendemoController(IGendemoService GendemoService)
        {
            _GendemoService = GendemoService;
        }

        /// <summary>
        /// 查询列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "gendemo:list")]
        public IActionResult Query([FromQuery] GendemoQueryDto parm)
        {
            //开始拼装查询条件
            var predicate = Expressionable.Create<Gendemo>();

            //TODO 搜索条件
            //predicate = predicate.And(m => m.Name.Contains(parm.Name));

            var response = _GendemoService.GetPages(predicate.ToExpression(), parm);

            return SUCCESS(response);
        }

        /// <summary>
        /// 查询详情
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [HttpGet("{Id}")]
        [ActionPermissionFilter(Permission = "gendemo:query")]
        public IActionResult Get(int Id)
        {
            var response = _GendemoService.GetId(Id);

            return SUCCESS(response);
        }

        /// <summary>
        /// 添加
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ActionPermissionFilter(Permission = "gendemo:add")]
        [Log(Title = "添加", BusinessType = BusinessType.INSERT)]
        public IActionResult Create([FromBody] GendemoDto parm)
        {
            if (parm == null)
            {
                throw new CustomException("请求参数错误");
            }
            //从 Dto 映射到 实体
            var addModel = parm.Adapt<Gendemo>().ToCreate();
            //addModel.CreateID = User.Identity.Name;

            return SUCCESS(_GendemoService.Add(addModel));
        }

        /// <summary>
        /// 更新
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [ActionPermissionFilter(Permission = "gendemo:update")]
        [Log(Title = "修改", BusinessType = BusinessType.UPDATE)]
        public IActionResult Update([FromBody] GendemoDto parm)
        {
            if (parm == null)
            {
                throw new CustomException("请求实体不能为空");
            }
            //从 Dto 映射到 实体
            var updateModel = parm.Adapt<Gendemo>().ToCreate();
            //updateModel.CreateID = User.Identity.Name;

            var response = _GendemoService.Update(w => w.Id == updateModel.Id, it => new Gendemo()
            {
                //TODO 字段映射
                Name = parm.Name,
                Icon = parm.Icon,
                ShowStatus = parm.ShowStatus,
                AddTime = parm.AddTime,

            });

            return SUCCESS(response);
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <returns></returns>
        [HttpDelete("{Id}")]
        [ActionPermissionFilter(Permission = "gendemo:delete")]
        [Log(Title = "删除", BusinessType = BusinessType.DELETE)]
        public IActionResult Delete(int Id = 0)
        {
            if (Id <= 0) { return OutputJson(ApiResult.Error($"删除失败Id 不能为空")); }

            // 删除
            var response = _GendemoService.Delete(Id);

            return SUCCESS(response);
        }
    }
}