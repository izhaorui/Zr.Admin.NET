using ZR.Model;
using ZR.Workflow.Model;

namespace ZR.Workflow.Service.IService
{
    public interface IWfWebhookService : IBaseService<WfWebhook>
    {
        /// <summary>分页/列表查询</summary>
        PagedInfo<WfWebhook> GetList(WfWebhook parm);

        /// <summary>新增</summary>
        WfWebhook Add(WfWebhook parm);

        /// <summary>修改</summary>
        new int Update(WfWebhook parm);

        /// <summary>删除（支持批量）</summary>
        int Delete(long[] ids);
    }
}
