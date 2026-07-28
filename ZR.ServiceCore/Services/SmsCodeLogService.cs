using Infrastructure;
using Infrastructure.Attribute;
using ZR.Infrastructure.Helper;
using ZR.Model;
using ZR.Model.Dto;
using ZR.Model.Models;
using ZR.Repository;
using ZR.ServiceCore.Sms;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 短信验证码记录Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(ISmsCodeLogService), ServiceLifetime = LifeTime.Transient)]
    public class SmsCodeLogService : BaseService<SmsCodeLog>, ISmsCodeLogService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly ISmsSender _smsSender;

        public SmsCodeLogService(ISmsSender smsSender)
        {
            _smsSender = smsSender;
        }

        /// <summary>
        /// 查询短信验证码记录列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<SmsCodeLogDto> GetList(SmscodeLogQueryDto parm)
        {
            var predicate = Expressionable.Create<SmsCodeLog>();

            predicate = predicate.AndIF(parm.Userid != null, it => it.Userid == parm.Userid);
            predicate = predicate.AndIF(parm.PhoneNum != null, it => it.PhoneNum == parm.PhoneNum);
            predicate = predicate.AndIF(parm.BeginAddTime == null, it => it.AddTime >= DateTime.Now.ToShortDateString().ParseToDateTime());
            predicate = predicate.AndIF(parm.BeginAddTime != null, it => it.AddTime >= parm.BeginAddTime);
            predicate = predicate.AndIF(parm.EndAddTime != null, it => it.AddTime <= parm.EndAddTime);
            predicate = predicate.AndIF(parm.SendType != null, it => it.SendType == parm.SendType);
            var response = Queryable()
                //.OrderBy("Id desc")
                .Where(predicate.ToExpression())
                .ToPage<SmsCodeLog, SmsCodeLogDto>(parm);

            return response;
        }


        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public SmsCodeLog GetInfo(long Id)
        {
            var response = Queryable()
                .Where(x => x.Id == Id)
                .First();

            return response;
        }

        /// <summary>
        /// 添加短信验证码记录
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public SmsCodeLog AddSmscodeLog(SmsCodeLog model)
        {
            model.AddTime = Context.GetDate();

            var smsCode = RandomHelper.GenerateNum(6);
            var smsContent = $"验证码{smsCode},有效期10分钟。";

            var oneMinus = Queryable().Any(f => f.PhoneNum == model.PhoneNum && SqlFunc.DateDiff(DateType.Minute, f.AddTime, model.AddTime) <= 1);
            if (oneMinus)
            {
                throw new CustomException("请稍后再试");
            }
            var oneMinusIP = Queryable().Any(f => f.UserIP == model.UserIP && SqlFunc.DateDiff(DateType.Minute, f.AddTime, model.AddTime) <= 1);
            if (oneMinusIP)
            {
                throw new CustomException("请稍后再试");
            }
            model.SmsCode = smsCode;
            model.SmsContent = smsContent;
            model.SendStatus = SmsSendStatus.Pending;
            // 先落库留痕（不依赖服务商结果），再发送
            model.Id = Context.Insertable(model).ExecuteReturnSnowflakeId();

            // 验证码短信：同步发送并回写结果（用户在等验证码，失败需立刻告知）
            var result = _smsSender.Send(new SmsMessage
            {
                PhoneNum = model.PhoneNum.ToString(),
                Content = smsContent,
                SendType = model.SendType,
                TemplateParams = new Dictionary<string, string> { { "code", smsCode } }
            });
            WriteBackSendResult(model, result);

            if (!result.Success)
            {
                throw new CustomException("短信发送失败，请稍后重试");
            }
            // 发送成功才写验证码缓存
            CacheService.SetPhoneCode(model.PhoneNum.ToString(), smsCode);
            return model;
        }

        /// <summary>
        /// 发送通知类短信（如订单发货通知）。
        /// 与 AddSmscodeLog 区别：不生成验证码、不做验证码频控、不写缓存。
        /// 先落库（SendStatus=待发送）后台异步发送，不阻塞业务流程；发送结果异步回写库。
        /// </summary>
        public SmsCodeLog SendSmsNotice(string phone, string content, int sendType = 6)
        {
            if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(content))
            {
                return null;
            }
            var message = new SmsMessage
            {
                PhoneNum = phone,
                Content = content,
                SendType = sendType
            };
            var model = InsertLog(message);

            // 通知类短信时效不敏感：fire-and-forget 异步发送，失败不影响主业务（如发货）
            var db = Context;
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var result = await _smsSender.SendAsync(message);
                    WriteBackSendResult(model, result, db);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"通知短信异步发送异常，Id={model.Id}，手机号={message.PhoneNum}");
                    try
                    {
                        WriteBackSendResult(model, SmsSendResult.Fail("EXCEPTION", ex.Message), db);
                    }
                    catch { /* 回写失败仅记日志 */ }
                }
            });
            return model;
        }

        /// <summary>
        /// 通用短信发送（落库 + 通过 ISmsSender 发送 + 回写发送结果），支持直发内容或模板发送。
        /// 同步等待服务商结果，调用方可拿到 Success/BizId/错误信息。
        /// </summary>
        public SmsSendResult SendSms(SmsMessage message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.PhoneNum))
            {
                return SmsSendResult.Fail("INVALID_PARAM", "手机号不能为空");
            }
            if (string.IsNullOrWhiteSpace(message.Content) && string.IsNullOrWhiteSpace(message.TemplateCode))
            {
                return SmsSendResult.Fail("INVALID_PARAM", "短信内容与模板编号至少填一项");
            }
            var model = InsertLog(message);
            var result = _smsSender.Send(message);
            WriteBackSendResult(model, result);
            return result;
        }

        /// <summary>
        /// 落库短信记录（SendStatus=待发送），不做验证码频控
        /// </summary>
        private SmsCodeLog InsertLog(SmsMessage message)
        {
            var model = new SmsCodeLog
            {
                SmsCode = string.Empty,
                Userid = 0,
                PhoneNum = message.PhoneNum.ParseToLong(),
                SmsContent = message.Content ?? $"[模板:{message.TemplateCode}]{JsonConvert.SerializeObject(message.TemplateParams)}",
                SendType = message.SendType,
                SendStatus = SmsSendStatus.Pending,
                UserIP = string.Empty,
                Location = string.Empty
            };
            model.Id = Context.Insertable(model).ExecuteReturnSnowflakeId();
            return model;
        }

        /// <summary>
        /// 发送后回写发送状态/回执ID/失败原因
        /// </summary>
        private void WriteBackSendResult(SmsCodeLog model, SmsSendResult result, ISqlSugarClient db = null)
        {
            var status = result.Success ? SmsSendStatus.Success : SmsSendStatus.Failed;
            var errorMsg = result.Success ? null : $"[{result.ErrorCode}]{result.ErrorMsg}";

            model.SendStatus = status;
            model.BizId = result.BizId;
            model.ErrorMsg = errorMsg;

            (db ?? Context).Updateable<SmsCodeLog>()
                .SetColumns(it => new SmsCodeLog { SendStatus = status, BizId = result.BizId, ErrorMsg = errorMsg })
                .Where(it => it.Id == model.Id)
                .ExecuteCommand();
        }
    }
}