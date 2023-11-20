using ZR.Model;
using ZR.Model.Dto;
using ZR.Service;
using ZR.ServiceCore.Model;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 邮件发送记录service接口
    /// </summary>
    public interface IEmailLogService : IBaseService<EmailLog>
    {
        PagedInfo<EmailLogDto> GetList(EmailLogQueryDto parm);

        EmailLog GetInfo(long Id);

        EmailLog AddEmailLog(EmailLog parm);

        int UpdateEmailLog(EmailLog parm);
    }
}
