using Infrastructure.Attribute;
using ZR.Model;
using ZR.Model.Dto;
using ZR.Repository;
using ZR.ServiceCore.Model;

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
        /// <param name="model"></param>
        /// <returns></returns>
        public EmailLog AddEmailLog(EmailLog model)
        {
            model.Id = Insertable(model).ExecuteReturnSnowflakeId();
            return model;
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