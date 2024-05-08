using ZR.Model;
using ZR.Model.Content;
using ZR.Model.Content.Dto;

namespace ZR.Service.Content.IService
{
    public interface IArticleCommentService
    {
        PagedInfo<ArticleCommentDto> GetMessageList(MessageQueryDto dto);
        ArticleComment AddMessage(ArticleComment message);
        int PraiseMessage(long mid);
        int DeleteMessage(long mid, long userId);
        PagedInfo<ArticleCommentDto> GetReplyComments(long mid, MessageQueryDto pager);
        PagedInfo<ArticleCommentDto> GetMyMessageList(MessageQueryDto dto);
        long TopMessage(long commentId, long top);
    }
}
