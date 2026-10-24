using ZR.Model;
using ZR.Model.Dto;
using ZR.Model.Models;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 邮件模板service接口
    /// </summary>
    public interface IEmailTplService : IBaseService<EmailTpl>
    {
        PagedInfo<EmailTplDto> GetList(EmailTplQueryDto parm);

        EmailTpl GetInfo(int Id);

        /// <summary>
        /// 按业务编码取启用中的模板（同 Code 多条时取最新一条）；未配置返回 null
        /// </summary>
        EmailTpl GetByCode(string code);

        EmailTpl AddEmailTpl(EmailTpl parm);

        int UpdateEmailTpl(EmailTpl parm);

    }
}
