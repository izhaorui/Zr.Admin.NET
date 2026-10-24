using Infrastructure.Attribute;
using Infrastructure.Model;
using ZR.Common.Email;
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
            predicate = predicate.AndIF(parm.Status != null, it => it.Status == parm.Status);
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.EmailType), it => it.EmailType == parm.EmailType);
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.FromEmail), it => it.FromEmail == parm.FromEmail);
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Subject), it => it.Subject.Contains(parm.Subject));
            predicate = predicate.AndIF(parm.BeginAddTime == null, it => it.AddTime >= DateTime.Now.AddDays(-7).ToShortDateString().ParseToDateTime());
            predicate = predicate.AndIF(parm.BeginAddTime != null, it => it.AddTime >= parm.BeginAddTime);
            predicate = predicate.AndIF(parm.EndAddTime != null, it => it.AddTime <= parm.EndAddTime);
            var response = Queryable()
                .Where(predicate.ToExpression())
                .OrderBy(x => x.Id, OrderByType.Desc)
                .ToPage<EmailLog, EmailLogDto>(parm);

            // 存量记录（新列补列前写入）Status/RetryCount 为 NULL：按 IsSend 归一，避免前端显示空白
            foreach (var item in response.Result)
            {
                item.Status ??= item.IsSend == 1 ? EmailSendStatus.Success : EmailSendStatus.Pending;
                item.RetryCount ??= 0;
            }

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
        /// 添加邮件发送记录（历史兼容方法）。
        /// 新流程请使用 <see cref="InsertLog"/> + <see cref="UpdateSendResult"/>，
        /// 本方法原来漏存 FileUrl / SendTime，导致从记录重发时附件丢失，此处已修正。
        /// </summary>
        /// <param name="sendEmailVo"></param>
        /// <param name="result">发送结果；"fail" 或空视为失败</param>
        /// <returns></returns>
        public EmailLog AddEmailLog(SendEmailDto sendEmailVo, string result)
        {
            var isFail = string.IsNullOrEmpty(result) || string.Equals(result, "fail", StringComparison.OrdinalIgnoreCase);
            var status = !sendEmailVo.IsSend
                ? EmailSendStatus.Draft
                : (isFail ? EmailSendStatus.Failed : EmailSendStatus.Success);

            var log = new EmailLog()
            {
                EmailContent = string.IsNullOrWhiteSpace(sendEmailVo.HtmlContent) ? sendEmailVo.Content : sendEmailVo.HtmlContent,
                Subject = sendEmailVo.Subject,
                ToEmails = sendEmailVo.ToUser,
                FileUrl = sendEmailVo.FileUrl,
                AddTime = DateTime.Now,
                FromEmail = sendEmailVo.FromEmail,
                FromName = sendEmailVo.FromName,
                IsSend = status == EmailSendStatus.Success ? 1 : 0,
                Status = status,
                ErrorCode = isFail && sendEmailVo.IsSend ? "SEND_ERROR" : null,
                SendResult = result,
                SendTime = status == EmailSendStatus.Success ? DateTime.Now : EmailLog.NoSendTime,
                EmailType = "manual",
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

        /// <summary>
        /// 落库一条发送记录（返回雪花 Id）
        /// </summary>
        public long InsertLog(EmailLog log)
        {
            return Insertable(log).ExecuteReturnSnowflakeId();
        }

        /// <summary>
        /// 回写发送结果。
        /// 成功：状态置成功、回填 SendTime/MessageId；失败：累计重试次数并按传入的 nextRetryTime 安排重试。
        /// </summary>
        public int UpdateSendResult(long id, EmailSendResult result, DateTime? nextRetryTime = null)
        {
            if (result == null)
            {
                return 0;
            }

            if (result.Success)
            {
                var sendResult = result.Simulated ? "simulated" : "ok";
                return Update(x => x.Id == id, x => new EmailLog()
                {
                    Status = EmailSendStatus.Success,
                    IsSend = 1,
                    SendResult = sendResult,
                    ErrorCode = "",
                    MessageId = result.MessageId,
                    NextRetryTime = null,
                    SendTime = DateTime.Now,
                });
            }

            var error = $"[{result.ErrorCode}]{result.ErrorMsg}";
            return Update(x => x.Id == id, x => new EmailLog()
            {
                Status = EmailSendStatus.Failed,
                IsSend = 0,
                SendResult = error,
                ErrorCode = result.ErrorCode,
                MessageId = result.MessageId,
                NextRetryTime = nextRetryTime,
                RetryCount = x.RetryCount + 1,
            });
        }

        /// <summary>
        /// 把指定记录重置为待发送（用于从发送记录重发）
        /// </summary>
        public int ResetForResend(long[] ids)
        {
            if (ids == null || ids.Length == 0) return 0;

            return Update(x => ids.Contains(x.Id), x => new EmailLog()
            {
                Status = EmailSendStatus.Pending,
                // 重发统一走后台队列：允许恢复与重试，且不会被当成同步直发
                DispatchMode = EmailDispatchMode.Queue,
                LastAttemptTime = null,
                IsSend = 0,
                RetryCount = 0,
                ErrorCode = null,
                SendResult = null,
                NextRetryTime = null,
            });
        }

        /// <summary>
        /// 按 Id 批量查询发送记录
        /// </summary>
        public List<EmailLog> GetListByIds(long[] ids)
        {
            if (ids == null || ids.Length == 0) return new List<EmailLog>();
            return Queryable().Where(x => ids.Contains(x.Id)).ToList();
        }
    }
}
