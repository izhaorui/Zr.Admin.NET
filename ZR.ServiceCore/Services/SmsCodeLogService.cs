using Infrastructure.Attribute;
using ZR.Model;
using ZR.Model.Dto;
using ZR.Repository;
using ZR.Service;
using ZR.ServiceCore.Model;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 短信验证码记录Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(ISmsCodeLogService), ServiceLifetime = LifeTime.Transient)]
    public class SmsCodeLogService : BaseService<SmsCodeLog>, ISmsCodeLogService
    {
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
            model.Id = Context.Insertable(model).ExecuteReturnSnowflakeId();
            return model;
        }
    }
}