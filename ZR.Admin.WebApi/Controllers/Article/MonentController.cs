using Microsoft.AspNetCore.Mvc;
using ZR.Admin.WebApi.Filters;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.ServiceCore.Model.Enums;

namespace ZR.Admin.WebApi.Controllers
{
    [Verify]
    [Route("monent")]
    public class MonentController : BaseController
    {
        /// <summary>
        /// 动态接口
        /// </summary>
        private readonly IArticleService _ArticleService;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ArticleService"></param>
        public MonentController(
            IArticleService ArticleService)
        {
            _ArticleService = ArticleService;
        }

        /// <summary>
        /// 查询我的
        /// </summary>
        /// <returns></returns>
        [HttpGet("mylist")]
        public IActionResult QueryMyList([FromQuery] ArticleQueryDto parm)
        {
            parm.UserId = HttpContext.GetUId();
            parm.ArticleType = 2;
            var response = _ArticleService.GetMyList(parm);

            return SUCCESS(response);
        }

        /// <summary>
        /// 查询动态列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("monentList")]
        [AllowAnonymous]
        public IActionResult QueryMonentList([FromQuery] ArticleQueryDto parm)
        {
            parm.UserId = HttpContext.GetUId();
            parm.ArticleType = 2;
            var response = _ArticleService.GetMonentList(parm);

            return SUCCESS(response);
        }

        /// <summary>
        /// 动态发布
        /// </summary>
        /// <returns></returns>
        [HttpPost("publishMonent")]
        public IActionResult PublishMonent([FromBody] ArticleDto parm)
        {
            var addModel = parm.Adapt<Article>().ToCreate(context: HttpContext);
            addModel.AuthorName = HttpContext.GetName();
            addModel.UserId = HttpContext.GetUId();
            addModel.ArticleType = ArticleTypeEnum.Monent;
            addModel.UserIP = HttpContext.GetClientUserIp();

            string location = HttpContextExtension.GetIpInfo(addModel.UserIP);
            addModel.Location = location;
            return SUCCESS(_ArticleService.InsertReturnIdentity(addModel));
        }

        /// <summary>
        /// 动态信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("getInfo")]
        public IActionResult GetInfo()
        {
            var userId = HttpContext.GetUId();

            var monentNum = _ArticleService.Queryable()
                .Count(f => f.UserId == userId && f.ArticleType == ArticleTypeEnum.Monent);

            return SUCCESS(new { monentNum, commentNum = 0 });
        }
    }
}
