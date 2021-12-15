﻿using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Model;
using Mapster;
using SqlSugar;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.Common;
using ZR.Model.Dto;
using ZR.Model.Models;
using ZR.Service.System.IService;

namespace ZR.Admin.WebApi.Controllers.System
{
    [Route("system/notice")]
    public class SysNoticeController : BaseController
    {
        /// <summary>
        /// 通知公告表接口
        /// </summary>
        private readonly ISysNoticeService _SysNoticeService;

        public SysNoticeController(ISysNoticeService SysNoticeService)
        {
            _SysNoticeService = SysNoticeService;
        }

        /// <summary>
        /// 查询通知公告表列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "system:sysnotice:list")]
        public IActionResult QuerySysNotice([FromQuery] SysNoticeQueryDto parm)
        {
            //开始拼装查询条件
            var predicate = Expressionable.Create<SysNotice>();

            //搜索条件查询语法参考Sqlsugar
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.NoticeTitle), m => m.NoticeTitle.Contains(parm.NoticeTitle));
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.NoticeType), m => m.NoticeType == parm.NoticeType);
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.CreateBy), m => m.Create_by.Contains(parm.CreateBy) ||  m.Update_by.Contains(parm.CreateBy));
            var response = _SysNoticeService.GetPages(predicate.ToExpression(), parm);
            return SUCCESS(response);
        }

        /// <summary>
        /// 查询通知公告表详情
        /// </summary>
        /// <param name="NoticeId"></param>
        /// <returns></returns>
        [HttpGet("{NoticeId}")]
        [ActionPermissionFilter(Permission = "system:sysnotice:query")]
        public IActionResult GetSysNotice(int NoticeId)
        {
            var response = _SysNoticeService.GetFirst(x => x.NoticeId == NoticeId);

            return SUCCESS(response);
        }

        /// <summary>
        /// 添加通知公告表
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ActionPermissionFilter(Permission = "system:sysnotice:add")]
        [Log(Title = "通知公告表", BusinessType = BusinessType.INSERT)]
        public IActionResult AddSysNotice([FromBody] SysNoticeDto parm)
        {
            if (parm == null)
            {
                throw new CustomException("请求参数错误");
            }
            //从 Dto 映射到 实体
            var model = parm.Adapt<SysNotice>().ToCreate(HttpContext);
            model.Create_by = User.Identity.Name;
            model.Create_time = DateTime.Now;

            return SUCCESS(_SysNoticeService.Insert(model, it => new
            {
                it.NoticeTitle,
                it.NoticeType,
                it.NoticeContent,
                it.Status,
                it.Remark,
                it.Create_by,
                it.Create_time
            }));
        }

        /// <summary>
        /// 更新通知公告表
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [ActionPermissionFilter(Permission = "system:sysnotice:update")]
        [Log(Title = "通知公告表", BusinessType = BusinessType.UPDATE)]
        public IActionResult UpdateSysNotice([FromBody] SysNoticeDto parm)
        {
            if (parm == null)
            {
                throw new CustomException("请求实体不能为空");
            }
            //从 Dto 映射到 实体
            var model = parm.Adapt<SysNotice>().ToUpdate(HttpContext);

            var response = _SysNoticeService.Update(w => w.NoticeId == model.NoticeId, it => new SysNotice()
            {
                //Update 字段映射
                NoticeTitle = model.NoticeTitle,
                NoticeType = model.NoticeType,
                NoticeContent = model.NoticeContent,
                Status = model.Status,
                Remark = model.Remark,
                Update_by = User.Identity.Name,
                Update_time = DateTime.Now
            });

            return SUCCESS(response);
        }

        /// <summary>
        /// 删除通知公告表
        /// </summary>
        /// <returns></returns>
        [HttpDelete("{ids}")]
        [ActionPermissionFilter(Permission = "system:sysnotice:delete")]
        [Log(Title = "通知公告表", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteSysNotice(string ids)
        {
            int[] idsArr = Tools.SpitIntArrary(ids);
            if (idsArr.Length <= 0) { return ToResponse(ApiResult.Error($"删除失败Id 不能为空")); }

            var response = _SysNoticeService.Delete(idsArr);

            return SUCCESS(response);
        }

        /// <summary>
        /// 通知公告表导出
        /// </summary>
        /// <returns></returns>
        [Log(BusinessType = BusinessType.EXPORT, IsSaveResponseData = false, Title = "通知公告表")]
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "system:sysnotice:export")]
        public IActionResult Export()
        {
            var list = _SysNoticeService.GetAll();

            string sFileName = ExportExcel(list, "SysNotice", "通知公告表");
            return SUCCESS(new { path = "/export/" + sFileName, fileName = sFileName });
        }
    }
}