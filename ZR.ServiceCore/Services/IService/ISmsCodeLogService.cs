using ZR.Model;
using ZR.Model.Dto;
using ZR.Model.Models;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 短信验证码记录service接口
    /// </summary>
    public interface ISmsCodeLogService : IBaseService<SmsCodeLog>
    {
        PagedInfo<SmsCodeLogDto> GetList(SmscodeLogQueryDto parm);

        SmsCodeLog GetInfo(long Id);

        SmsCodeLog AddSmscodeLog(SmsCodeLog parm);

    }
}
