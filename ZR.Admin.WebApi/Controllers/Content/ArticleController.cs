using Microsoft.AspNetCore.Mvc;
using MiniExcelLibs;
using ZR.Model.Content;
using ZR.Model.Content.Dto;
using ZR.Service.Content.IService;

namespace ZR.Admin.WebApi.Controllers.Content
{
    /// <summary>
    /// 内容管理
    /// </summary>
    [Route("article")]
    [ApiExplorerSettings(GroupName = "article")]
    public class ArticleController : BaseController
    {
        /// <summary>
        /// 文章接口
        /// </summary>
        private readonly IArticleService _ArticleService;
        private readonly IArticleCategoryService _ArticleCategoryService;

        public ArticleController(
            IArticleService ArticleService,
            IArticleCategoryService articleCategoryService)
        {
            _ArticleService = ArticleService;
            _ArticleCategoryService = articleCategoryService;
            _ArticleService = ArticleService;
        }

        /// <summary>
        /// 查询文章列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "system:article:list")]
        public IActionResult Query([FromQuery] ArticleQueryDto parm)
        {
            var response = _ArticleService.GetList(parm);

            return SUCCESS(response);
        }

        /// <summary>
        /// 内容批量审核通过
        /// </summary>
        /// <returns></returns>
        [HttpPut("pass/{ids}")]
        [ActionPermissionFilter(Permission = "article:audit")]
        [Log(Title = "内容审核", BusinessType = BusinessType.UPDATE)]
        public IActionResult PassedMonents(string ids)
        {
            long[] idsArr = Tools.SpitLongArrary(ids);
            if (idsArr.Length <= 0) { return ToResponse(ApiResult.Error($"审核通过失败Id 不能为空")); }

            return ToResponse(_ArticleService.Passed(idsArr));
        }

        /// <summary>
        /// 内容批量审核拒绝
        /// </summary>
        /// <returns></returns>
        [HttpPut("reject/{ids}")]
        [ActionPermissionFilter(Permission = "article:audit")]
        [Log(Title = "内容审核", BusinessType = BusinessType.UPDATE)]
        public IActionResult RejectMonents(string ids, string reason = "")
        {
            long[] idsArr = Tools.SpitLongArrary(ids);
            if (idsArr.Length <= 0) { return ToResponse(ApiResult.Error($"審核拒绝失败Id 不能为空")); }

            int result = _ArticleService.Reject(reason, idsArr);
            return ToResponse(result);
        }

        /// <summary>
        /// 查询我的文章列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("mylist")]
        public IActionResult QueryMyList([FromQuery] ArticleQueryDto parm)
        {
            parm.UserId = HttpContext.GetUId();
            var response = _ArticleService.GetMyList(parm);

            return SUCCESS(response);
        }

        /// <summary>
        /// 查询文章详情
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public IActionResult Get(int id)
        {
            long userId = HttpContext.GetUId();
            var model = _ArticleService.GetArticle(id, userId);

            ApiResult apiResult = ApiResult.Success(model);

            return ToResponse(apiResult);
        }

        /// <summary>
        /// 发布文章
        /// </summary>
        /// <returns></returns>
        [HttpPost("add")]
        [ActionPermissionFilter(Permission = "system:article:add")]
        //[Log(Title = "发布文章", BusinessType = BusinessType.INSERT)]
        public IActionResult Publish([FromBody] ArticleDto parm)
        {
            var addModel = parm.Adapt<Article>().ToCreate(context: HttpContext);
            
            return SUCCESS(_ArticleService.PublishArticle(addModel));
        }

        /// <summary>
        /// 更新文章
        /// </summary>
        /// <returns></returns>
        [HttpPut("edit")]
        [ActionPermissionFilter(Permission = "system:article:update")]
        //[Log(Title = "文章修改", BusinessType = BusinessType.UPDATE)]
        public IActionResult Update([FromBody] ArticleDto parm)
        {
            parm.AuthorName = HttpContext.GetName();
            var modal = parm.Adapt<Article>().ToUpdate(HttpContext);
            var response = _ArticleService.UpdateArticle(modal);

            return SUCCESS(response);
        }

        /// <summary>
        /// 置顶
        /// </summary>
        /// <returns></returns>
        [HttpPut("top")]
        [ActionPermissionFilter(Permission = "system:article:update")]
        [Log(Title = "置顶文章", BusinessType = BusinessType.UPDATE)]
        public IActionResult Top([FromBody] Article parm)
        {
            var response = _ArticleService.TopArticle(parm);

            return SUCCESS(response);
        }

        /// <summary>
        /// 是否公开
        /// </summary>
        /// <returns></returns>
        [HttpPut("changePublic")]
        [ActionPermissionFilter(Permission = "system:article:update")]
        [Log(Title = "是否公开", BusinessType = BusinessType.UPDATE)]
        public IActionResult ChangePublic([FromBody] Article parm)
        {
            var response = _ArticleService.ChangeArticlePublic(parm);

            return SUCCESS(response);
        }

        /// <summary>
        /// 删除文章
        /// </summary>
        /// <returns></returns>
        [HttpDelete("{id}")]
        [ActionPermissionFilter(Permission = "system:article:delete")]
        [Log(Title = "文章删除", BusinessType = BusinessType.DELETE)]
        public IActionResult Delete(int id = 0)
        {
            var response = _ArticleService.Delete(id);
            return SUCCESS(response);
        }

        /// <summary>
        /// 导出文章
        /// </summary>
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "system:article:export")]
        [Log(Title = "文章导出", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        public IActionResult Export([FromQuery] ArticleQueryDto parm)
        {
            parm.PageNum = 1;
            parm.PageSize = 100000;
            var list = _ArticleService.ExportList(parm);
            if (list == null || list.Count <= 0)
            {
                return ToResponse(ResultCode.FAIL, "没有要导出的数据");
            }

            var result = ExportExcelMini(list, "article", "文章内容");
            return ExportExcel(result.Item2, result.Item1);
        }

        /// <summary>
        /// 导入文章
        /// </summary>
        [HttpPost("importData")]
        [Consumes("multipart/form-data")]
        [ActionPermissionFilter(Permission = "system:article:import")]
        [Log(Title = "文章导入", BusinessType = BusinessType.IMPORT, IsSaveRequestData = false)]
        public IActionResult ImportData(IFormFile file)
        {
            if (file == null || file.Length <= 0)
            {
                return ToResponse(ResultCode.FAIL, "请上传导入文件");
            }

            List<ArticleImportDto> list;
            using (var stream = file.OpenReadStream())
            {
                list = stream.Query<ArticleImportDto>(startCell: "A1")
                    .Where(x => !string.IsNullOrWhiteSpace(x.Title) || !string.IsNullOrWhiteSpace(x.Content))
                    .ToList();
            }

            if (list.Count <= 0)
            {
                return ToResponse(ResultCode.FAIL, "导入失败：未读取到有效数据，请使用系统模板并从第2行开始填写");
            }

            return SUCCESS(_ArticleService.ImportArticle(list));
        }

        /// <summary>
        /// 文章导入模板下载
        /// </summary>
        [HttpGet("importTemplate")]
        [ActionPermissionFilter(Permission = "system:article:import")]
        [Log(Title = "文章导入模板", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        public IActionResult ImportTemplateExcel()
        {
            var result = DownloadImportTemplate(new List<ArticleImportDto>() { }, "article");
            return ExportExcel(result.Item2, result.Item1);
        }
    }
}