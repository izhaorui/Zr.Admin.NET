using Infrastructure.Model;
using ZR.Common.Email;
using ZR.Model;
using ZR.Model.Dto;
using ZR.Model.Models;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 邮件发送记录service接口
    /// </summary>
    public interface IEmailLogService : IBaseService<EmailLog>
    {
        PagedInfo<EmailLogDto> GetList(EmailLogQueryDto parm);

        EmailLog GetInfo(long Id);

        EmailLog AddEmailLog(SendEmailDto parm, string result);

        int UpdateEmailLog(EmailLog parm);

        /// <summary>落库一条发送记录（返回雪花 Id），供发送流程使用</summary>
        long InsertLog(EmailLog log);

        /// <summary>回写发送结果（成功置成功状态；失败累计重试次数并记录下次重试时间）</summary>
        int UpdateSendResult(long id, EmailSendResult result, DateTime? nextRetryTime = null);

        /// <summary>把指定记录重置为待发送（用于从发送记录重发）</summary>
        int ResetForResend(long[] ids);

        /// <summary>按 Id 批量查询发送记录</summary>
        List<EmailLog> GetListByIds(long[] ids);
    }
}
