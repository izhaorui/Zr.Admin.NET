using Microsoft.AspNetCore.Mvc;
using ZR.Admin.WebApi.Filters;
using ZR.Model;
using ZR.ServiceCore.Model;
using ZR.ServiceCore.Model.Dto;
using ZR.ServiceCore.Services.IBusinessService;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 评论
    /// </summary>
    [Route("front/comment")]
    [ApiController]
    public class FrontCommentController : BaseController
    {
        private readonly IArticleCommentService messageService;
        public FrontCommentController(IArticleCommentService messageService)
        {
            this.messageService = messageService;
        }

        /// <summary>
        /// 查询评论列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        public IActionResult QueryList([FromQuery] MessageQueryDto parm)
        {
            PagedInfo<ArticleCommentDto>? response;
            //查询二级评论
            if (parm.CommentId > 0)
            {
                response = messageService.GetReplyComments(parm.CommentId, parm);
            }
            else
            {
                response = messageService.GetMessageList(parm);
            }

            return SUCCESS(response);
        }

        /// <summary>
        /// 评论
        /// </summary>
        /// <returns></returns>
        [HttpPost("add")]
        [Verify]
        public IActionResult Create([FromBody] ArticleCommentDto parm)
        {
            var uid = HttpContextExtension.GetUId(HttpContext);
            if (uid <= 0) { return ToResponse(ResultCode.DENY); }

            var addModel = parm.Adapt<ArticleComment>().ToCreate(context: HttpContext);
            addModel.UserIP = HttpContextExtension.GetClientUserIp(HttpContext);
            addModel.UserId = uid;
            return SUCCESS(messageService.AddMessage(addModel).Adapt<ArticleCommentDto>());
        }

        /// <summary>
        /// 评论点赞
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("praise")]
        [Verify]
        public IActionResult Praise([FromBody] ArticleCommentDto dto)
        {
            if(dto == null || dto.CommentId <= 0) return ToResponse(ResultCode.PARAM_ERROR);
            //var uid = HttpContextExtension.GetUId(HttpContext);
            
            return SUCCESS(messageService.PraiseMessage(dto.CommentId));
        }

        /// <summary>
        /// 评论删除
        /// </summary>
        /// <param name="mid"></param>
        /// <returns></returns>
        [HttpDelete("delete/{mid}")]
        [Verify]
        public IActionResult Delete(string mid)
        {
            var uid = HttpContextExtension.GetUId(HttpContext);
            if (uid <= 0) { return ToResponse(ResultCode.DENY); }
            return SUCCESS(messageService.DeleteMessage(mid.ParseToLong(), uid));
        }

        /// <summary>
        /// 查询我的评论列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("mylist")]
        [Verify]
        public IActionResult QueryMyCommentList([FromQuery] MessageQueryDto parm)
        {
            PagedInfo<ArticleCommentDto> response = messageService.GetMyMessageList(parm);

            return SUCCESS(response);
        }
    }
}
