using Infrastructure.Attribute;
using Mapster;
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
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Code), it => it.Code == parm.Code);
            predicate = predicate.AndIF(parm.Enabled != null, it => it.Enabled == parm.Enabled);
            var page = Queryable()
                .Where(predicate.ToExpression())
                .OrderBy(x => x.Id, OrderByType.Desc)
                .ToPage<EmailTpl>(parm);

            // 存量模板（补列前创建）IsHtml/Enabled 为 NULL：按「启用 + HTML」归一
            foreach (var item in page.Result)
            {
                item.IsHtml ??= true;
                item.Enabled ??= true;
            }

            return new PagedInfo<EmailTplDto>
            {
                PageIndex = page.PageIndex,
                PageSize = page.PageSize,
                TotalNum = page.TotalNum,
                Result = page.Result.Adapt<List<EmailTplDto>>(),
            };
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

            if (response != null)
            {
                response.IsHtml ??= true;
                response.Enabled ??= true;
            }

            return response;
        }

        /// <summary>
        /// 按业务编码取启用中的模板（同 Code 多条时取最新一条）
        /// </summary>
        public EmailTpl GetByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            // Enabled 为 NULL 视为启用（兼容存量数据；SQL 里 NULL <> 0 不成立，必须显式判空）
            return Queryable()
                .Where(x => x.Code == code.Trim() && (x.Enabled == null || x.Enabled == true))
                .OrderBy(x => x.Id, OrderByType.Desc)
                .First();
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
