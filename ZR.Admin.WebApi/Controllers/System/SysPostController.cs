using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System.Collections.Generic;
using ZR.Admin.WebApi.Filters;
using ZR.Model;
using ZR.Model.System;
using Infrastructure.Extensions;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure;
using ZR.Service.System.IService;
using ZR.Common;
using ZR.Admin.WebApi.Extensions;

namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 岗位管理
    /// </summary>
    [Verify]
    [Route("system/post")]
    public class SysPostController : BaseController
    {
        private readonly ISysPostService PostService;
        public SysPostController(ISysPostService postService)
        {
            PostService = postService;
        }

        /// <summary>
        /// 岗位列表查询
        /// </summary>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "system:post:list")]
        public IActionResult List([FromQuery] SysPost post, [FromQuery] PagerInfo pagerInfo)
        {
            var predicate = Expressionable.Create<SysPost>();
            predicate = predicate.AndIF(post.Status.IfNotEmpty(), it => it.Status == post.Status);
            var list = PostService.GetPages(predicate.ToExpression(), pagerInfo, s => new { s.PostSort });

            return SUCCESS(list);
        }

        /// <summary>
        /// 岗位查询
        /// </summary>
        /// <param name="postId"></param>
        /// <returns></returns>
        [HttpGet("{postId}")]
        [ActionPermissionFilter(Permission = "system:post:query")]
        public IActionResult Query(long postId = 0)
        {
            return SUCCESS(PostService.GetId(postId));
        }

        /// <summary>
        /// 岗位管理
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        [HttpPost]
        [ActionPermissionFilter(Permission = "system:post:add")]
        [Log(Title = "岗位添加", BusinessType = BusinessType.INSERT)]
        public IActionResult Add([FromBody] SysPost post)
        {
            if (UserConstants.NOT_UNIQUE.Equals(PostService.CheckPostNameUnique(post)))
            {
                throw new CustomException($"修改岗位{post.PostName}失败，岗位名已存在");
            }
            if (UserConstants.NOT_UNIQUE.Equals(PostService.CheckPostCodeUnique(post)))
            {
                throw new CustomException($"修改岗位{post.PostName}失败，岗位编码已存在");
            }

            post.Create_by = HttpContext.GetName();
            return ToResponse(ToJson(PostService.Add(post)));
        }

        /// <summary>
        /// 岗位管理
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        [HttpPut]
        [ActionPermissionFilter(Permission = "system:post:edit")]
        [Log(Title = "岗位编辑", BusinessType = BusinessType.UPDATE)]
        public IActionResult Update([FromBody] SysPost post)
        {
            if (UserConstants.NOT_UNIQUE.Equals(PostService.CheckPostNameUnique(post)))
            {
                throw new CustomException($"修改岗位{post.PostName}失败，岗位名已存在");
            }
            if (UserConstants.NOT_UNIQUE.Equals(PostService.CheckPostCodeUnique(post)))
            {
                throw new CustomException($"修改岗位{post.PostName}失败，岗位编码已存在");
            }
            post.Update_by = HttpContext.GetName();
            return ToResponse(PostService.Update(post));
        }

        /// <summary>
        /// 岗位删除
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        [ActionPermissionFilter(Permission = "system:post:remove")]
        [Log(Title = "岗位删除", BusinessType = BusinessType.DELETE)]
        public IActionResult Delete(string id)
        {
            int[] ids = Tools.SpitIntArrary(id);
            return ToResponse(ToJson(PostService.Delete(ids)));
        }

        /// <summary>
        /// 获取岗位选择框列表
        /// </summary>
        [HttpGet("optionselect")]
        public IActionResult Optionselect()
        {
            List<SysPost> posts = PostService.GetAll();
            return SUCCESS(posts);
        }

        /// <summary>
        /// 岗位导出
        /// </summary>
        /// <returns></returns>
        [Log(BusinessType = BusinessType.EXPORT, IsSaveResponseData = false, Title= "岗位导出")]
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "system:post:export")]
        public IActionResult Export()
        {
            var list = PostService.GetAll();

            string sFileName = ExportExcel(list, "syspost", "岗位");
            return SUCCESS(new { path = "/export/" + sFileName, fileName = sFileName });
        }
    }
}
