using ZR.Model;
using ZR.ServiceCore.Model;
using ZR.ServiceCore.Model.Dto;

namespace ZR.ServiceCore.Services.IBusinessService
{
    public interface IArticleCommentService
    {
        PagedInfo<ArticleCommentDto> GetMessageList(MessageQueryDto dto);
        ArticleComment AddMessage(ArticleComment message);
        int PraiseMessage(long mid);
        int DeleteMessage(long mid, long userId);
        PagedInfo<ArticleCommentDto> GetReplyComments(long mid, MessageQueryDto pager);
        PagedInfo<ArticleCommentDto> GetMyMessageList(MessageQueryDto dto);
    }
}
