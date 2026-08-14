using System;
using SqlSugar;
using ZR.Model;
using ZR.Workflow.Model;
using ZR.Workflow.Service.IService;

namespace ZR.Workflow.Service
{
    /// <summary>
    /// 工作流 Webhook 端点配置服务（CRUD）。
    /// 配置为全局共享，节点通过 EnterWebhookId / LeaveWebhookId 引用。
    /// </summary>
    [AppService(ServiceType = typeof(IWfWebhookService))]
    public class WfWebhookService : BaseService<WfWebhook>, IWfWebhookService
    {
        public PagedInfo<WfWebhook> GetList(WfWebhook parm)
        {
            var predicate = Expressionable.Create<WfWebhook>()
                .AndIF(!string.IsNullOrEmpty(parm?.Name), it => it.Name.Contains(parm.Name))
                .AndIF(!string.IsNullOrEmpty(parm?.Url), it => it.Url.Contains(parm.Url));
            var list = Queryable()
                .Where(predicate.ToExpression())
                .OrderByDescending(it => it.Create_time)
                .ToList();
            return new PagedInfo<WfWebhook> { Result = list, PageIndex = 1, PageSize = 50, TotalNum = list.Count };
        }

        public WfWebhook Add(WfWebhook parm)
        {
            if (Queryable().Any(it => it.Name == parm.Name))
                throw new CustomException(ResultCode.CUSTOM_ERROR, $"Webhook 名称「{parm.Name}」已存在", null);
            if (Queryable().Any(it => it.Url == parm.Url))
                throw new CustomException(ResultCode.CUSTOM_ERROR, "该回调地址已存在", null);
            if (parm.Enabled == 0) parm.Enabled = 0; // 允许显式停用
            parm.Enabled = parm.Enabled == 0 ? 0 : 1;
            var entity = parm.ToCreate(App.HttpContext);
            return InsertReturnEntity(entity);
        }

        public new int Update(WfWebhook parm)
        {
            var existing = GetFirst(it => it.WebhookId == parm.WebhookId)
                ?? throw new CustomException(ResultCode.CUSTOM_ERROR, "Webhook 配置不存在", null);
            if (Queryable().Any(it => it.WebhookId != parm.WebhookId && it.Name == parm.Name))
                throw new CustomException(ResultCode.CUSTOM_ERROR, $"Webhook 名称「{parm.Name}」已存在", null);
            if (Queryable().Any(it => it.WebhookId != parm.WebhookId && it.Url == parm.Url))
                throw new CustomException(ResultCode.CUSTOM_ERROR, "该回调地址已存在", null);
            existing.Name = parm.Name;
            existing.Url = parm.Url;
            existing.Remark = parm.Remark;
            existing.Enabled = parm.Enabled == 0 ? 0 : 1;
            existing.ToUpdate(App.HttpContext);
            return Update(existing, true, "修改 Webhook 配置");
        }

        public int Delete(long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return 0;
            return Context.Deleteable<WfWebhook>().Where(it => ids.Contains(it.WebhookId)).ExecuteCommand();
        }
    }
}
