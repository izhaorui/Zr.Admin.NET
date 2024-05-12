using Infrastructure.Attribute;
using ZR.Model;
using ZR.Model.Dto;
using ZR.Model.Models;
using ZR.Repository;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 邮件模板Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(IEmailTplService), ServiceLifetime = LifeTime.Transient)]
    public class EmailTplService : BaseService<EmailTpl>, IEmailTplService
    {
        /// <summary>
        /// 查询邮件模板列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<EmailTplDto> GetList(EmailTplQueryDto parm)
        {
            var predicate = Expressionable.Create<EmailTpl>();

            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Name), it => it.Name == parm.Name);
            var response = Queryable()
                //.OrderBy("Id desc")
                .Where(predicate.ToExpression())
                .ToPage<EmailTpl, EmailTplDto>(parm);

            return response;
        }


        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public EmailTpl GetInfo(int Id)
        {
            var response = Queryable()
                .Where(x => x.Id == Id)
                .First();

            return response;
        }

        /// <summary>
        /// 添加邮件模板
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public EmailTpl AddEmailTpl(EmailTpl model)
        {
            return Context.Insertable(model).ExecuteReturnEntity();
        }

        /// <summary>
        /// 修改邮件模板
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public int UpdateEmailTpl(EmailTpl model)
        {
            return Update(model, true);
        }

    }
}