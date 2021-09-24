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
            //开始拼装查询条件
            var predicate = Expressionable.Create<SysPost>();
            predicate = predicate.AndIF(post.Status.IfNotEmpty(), it => it.Status == post.Status);
            var list = PostService.GetPages(predicate.ToExpression(), pagerInfo);

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
            post.Update_by = User.Identity.Name;
            return OutputJson(ToJson(PostService.Add(post)));
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
            post.Update_by = User.Identity.Name;
            return OutputJson(ToJson(PostService.Update(post)));
        }

        /// <summary>
        /// 岗位删除
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        [ActionPermissionFilter(Permission = "system:post:remove")]
        [Log(Title = "岗位删除", BusinessType = BusinessType.DELETE)]
        public IActionResult Delete(int id = 0)
        {
            return OutputJson(ToJson(PostService.Delete(id)));
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
    }
}
