using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Extensions;
using Infrastructure.Model;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using ZR.Admin.WebApi.Filters;
using ZR.Common;
using ZR.Model;
using ZR.Model.Dto;
using ZR.Model.Models;
using ZR.Service.System.ISystemService;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 多语言配置Controller
    /// </summary>
    [Verify]
    [Route("system/CommonLang")]
    public class CommonLangController : BaseController
    {
        /// <summary>
        /// 多语言配置接口
        /// </summary>
        private readonly ICommonLangService _CommonLangService;

        public CommonLangController(ICommonLangService CommonLangService)
        {
            _CommonLangService = CommonLangService;
        }

        /// <summary>
        /// 查询多语言配置列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "system:lang:list")]
        public IActionResult QueryCommonLang([FromQuery] CommonLangQueryDto parm)
        {
            if (parm.ShowMode == 2)
            {
                PagedInfo<dynamic> pagedInfo = new();
                pagedInfo.Result = _CommonLangService.GetListToPivot(parm);

                return SUCCESS(pagedInfo);
            }

            return SUCCESS(_CommonLangService.GetList(parm));
        }

        /// <summary>
        /// 查询多语言配置列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("list/{lang}")]
        [AllowAnonymous]
        public IActionResult QueryCommonLangs()
        {
            var msgList = _CommonLangService.GetLangList(new CommonLangQueryDto() { LangCode = "zh-cn" });
            var msgListEn = _CommonLangService.GetLangList(new CommonLangQueryDto() { LangCode = "en" });
            var msgListTw = _CommonLangService.GetLangList(new CommonLangQueryDto() { LangCode = "zh-tw" });
            Dictionary<string, object> dic = new();
            Dictionary<string, object> dicEn = new();
            Dictionary<string, object> dicTw = new();
            SetLang(msgList, dic);
            SetLang(msgListEn, dicEn);
            SetLang(msgListTw, dicTw);
            return SUCCESS(new
            {
                en = dicEn,
                cn = dic,
                tw = dicTw
            });
        }

        private static void SetLang(List<CommonLang> msgList, Dictionary<string, object> dic)
        {
            foreach (var item in msgList)
            {
                if (!dic.ContainsKey(item.LangKey))
                {
                    dic.Add(item.LangKey, item.LangName);
                }
            }
        }

        /// <summary>
        /// 查询多语言配置详情
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [HttpGet("{Id}")]
        [ActionPermissionFilter(Permission = "system:lang:query")]
        public IActionResult GetCommonLang(long Id)
        {
            var response = _CommonLangService.GetFirst(x => x.Id == Id);

            var list = _CommonLangService.GetList(x => x.LangKey == response.LangKey);
            var vo = list.Adapt<List<CommonLangDto>>();
            var modal = new CommonLangDto() { LangKey = response.LangKey, LangList = vo };
            return SUCCESS(modal);
        }

        /// <summary>
        /// 查询多语言配置详情
        /// </summary>
        /// <param name="langKey"></param>
        /// <returns></returns>
        [HttpGet("key/{langKey}")]
        [ActionPermissionFilter(Permission = "system:lang:query")]
        public IActionResult GetCommonLangByKey(string langKey)
        {
            var list = _CommonLangService.GetList(x => x.LangKey == langKey);
            var vo = list.Adapt<List<CommonLangDto>>();
            var modal = new CommonLangDto() { LangKey = langKey, LangList = vo };

            return SUCCESS(modal);
        }

        /// <summary>
        /// 更新多语言配置
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [ActionPermissionFilter(Permission = "system:lang:edit")]
        [Log(Title = "多语言配置", BusinessType = BusinessType.UPDATE)]
        public IActionResult UpdateCommonLang([FromBody] CommonLangDto parm)
        {
            if (parm == null || parm.LangKey.IsEmpty())
            {
                throw new CustomException("请求实体不能为空");
            }

            List<CommonLang> langs = new();
            foreach (var item in parm.LangList)
            {
                langs.Add(new CommonLang() { Addtime = DateTime.Now, LangKey = parm.LangKey, LangCode = item.LangCode, LangName = item.LangName });
            }
            var storage = _CommonLangService.Storageable(langs).WhereColumns(it => new { it.LangKey, it.LangCode }).ToStorage();

            long r = storage.AsInsertable.ExecuteReturnSnowflakeId();//执行插入
            storage.AsUpdateable.UpdateColumns(it => new { it.LangName }).ExecuteCommand();//执行修改

            return ToResponse(r);
        }

        /// <summary>
        /// 删除多语言配置
        /// </summary>
        /// <returns></returns>
        [HttpDelete("{ids}")]
        [ActionPermissionFilter(Permission = "system:lang:delete")]
        [Log(Title = "多语言配置", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteCommonLang(string ids)
        {
            long[] idsArr = Tools.SpitLongArrary(ids);
            if (idsArr.Length <= 0) { return ToResponse(ApiResult.Error($"删除失败Id 不能为空")); }

            var response = _CommonLangService.Delete(idsArr);

            return ToResponse(response);
        }

        /// <summary>
        /// 导出多语言配置
        /// </summary>
        /// <returns></returns>
        [Log(Title = "多语言配置", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "system:lang:export")]
        public IActionResult Export([FromQuery] CommonLangQueryDto parm)
        {
            parm.PageSize = 10000;
            var list = _CommonLangService.GetList(parm).Result;

            string sFileName = ExportExcel(list, "CommonLang", "多语言配置");
            return SUCCESS(new { path = "/export/" + sFileName, fileName = sFileName });
        }

    }
}