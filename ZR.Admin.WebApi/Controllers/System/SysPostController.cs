using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using ZR.Admin.WebApi.Filters;
using ZR.Model.System;
using ZR.Repository;
using ZR.ServiceCore.Model.Dto;

namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 岗位管理
    /// </summary>
    [Verify]
    [Route("system/post")]
    [ApiExplorerSettings(GroupName = "sys")]
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
        public IActionResult List([FromQuery] SysPostQueryDto dto)
        {
            var predicate = Expressionable.Create<SysPost>();
            predicate = predicate.AndIF(dto.Status.IfNotEmpty(), it => it.Status == dto.Status);
            predicate = predicate.AndIF(dto.PostName.IfNotEmpty(), it => it.PostName.Contains(dto.PostName));
            predicate = predicate.AndIF(dto.PostCode.IfNotEmpty(), it => it.PostCode.Contains(dto.PostCode));

            var list = PostService.Queryable()
             .Where(predicate.ToExpression())
                .Select((it) => new SysPostDto
                {
                    UserNum = SqlFunc.Subqueryable<SysUserPost>().Where(f => f.PostId == it.PostId).Sum(f => f.UserId)
                }, true)
                .ToPage(dto);

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
            post.ToCreate(HttpContext);

            return ToResponse(PostService.Add(post));
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
            post.ToUpdate(HttpContext);
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
            return ToResponse(PostService.Delete(ids));
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
        [Log(BusinessType = BusinessType.EXPORT, IsSaveResponseData = false, Title = "岗位导出")]
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "system:post:export")]
        public IActionResult Export()
        {
            var list = PostService.GetAll();

            var result = ExportExcelMini(list, "post", "岗位列表");
            return ExportExcel(result.Item2, result.Item1);
        }
    }
}
