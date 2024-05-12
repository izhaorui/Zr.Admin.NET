using Infrastructure.Attribute;
using Infrastructure.Model;
using ZR.Model;
using ZR.Model.Dto;
using ZR.Model.Models;
using ZR.Repository;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 邮件发送记录Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(IEmailLogService), ServiceLifetime = LifeTime.Transient)]
    public class EmailLogService : BaseService<EmailLog>, IEmailLogService
    {
        /// <summary>
        /// 查询邮件发送记录列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<EmailLogDto> GetList(EmailLogQueryDto parm)
        {
            var predicate = Expressionable.Create<EmailLog>();

            predicate = predicate.AndIF(parm.IsSend != null, it => it.IsSend == parm.IsSend);
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.FromEmail), it => it.FromEmail == parm.FromEmail);
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Subject), it => it.Subject.Contains(parm.Subject));
            predicate = predicate.AndIF(parm.BeginAddTime == null, it => it.AddTime >= DateTime.Now.AddDays(-7).ToShortDateString().ParseToDateTime());
            predicate = predicate.AndIF(parm.BeginAddTime != null, it => it.AddTime >= parm.BeginAddTime);
            predicate = predicate.AndIF(parm.EndAddTime != null, it => it.AddTime <= parm.EndAddTime);
            var response = Queryable()
                .Where(predicate.ToExpression())
                .ToPage<EmailLog, EmailLogDto>(parm);

            return response;
        }

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public EmailLog GetInfo(long Id)
        {
            var response = Queryable()
                .Where(x => x.Id == Id)
                .First();

            return response;
        }

        /// <summary>
        /// 添加邮件发送记录
        /// </summary>
        /// <param name="sendEmailVo"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public EmailLog AddEmailLog(SendEmailDto sendEmailVo, string result)
        {
            var log = new EmailLog()
            {
                EmailContent = sendEmailVo.HtmlContent,
                Subject = sendEmailVo.Subject,
                ToEmails = sendEmailVo.ToUser,
                AddTime = DateTime.Now,
                FromEmail = sendEmailVo.FromEmail,
                IsSend = sendEmailVo.IsSend ? 1 : 0,
                SendResult = result,
                FromName = sendEmailVo.FromName,
            };
            log.Id = Insertable(log).ExecuteReturnSnowflakeId();
            return log;
        }

        /// <summary>
        /// 修改邮件发送记录
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public int UpdateEmailLog(EmailLog model)
        {
            return Update(model, true);
        }
    }
}