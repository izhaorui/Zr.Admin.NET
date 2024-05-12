using Infrastructure.Model;
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
    }
}
