using ZR.Model;

namespace ZR.Workflow.Model
{
    /// <summary>
    /// 工作流 Webhook 端点配置。
    /// 节点不再直接配置裸 URL，而是引用此表中的一条配置（EnterWebhookId / LeaveWebhookId），
    /// 投递记录（WfWebhookDelivery）保存 WebhookId + URL/Name 快照以备追踪。
    /// </summary>
    [SugarTable("wf_webhook")]
    [Tenant("0")]
    public class WfWebhook : SysBase
    {
        /// <summary>配置Id（主键）</summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long WebhookId { get; set; }

        /// <summary>配置名称（如 ERP、OA 审批中心），用于节点面板下拉展示与记录追踪</summary>
        [SugarColumn(Length = 100, IsNullable = false)]
        public string Name { get; set; }

        /// <summary>回调地址（节点进入/离开时 POST 此地址）</summary>
        [SugarColumn(Length = 500, IsNullable = false)]
        public string Url { get; set; }

        /// <summary>是否启用：1=启用 0=停用（停用后引用它的节点不再投递）</summary>
        [SugarColumn(IsNullable = false, DefaultValue = "1")]
        public int Enabled { get; set; } = 1;
    }
}
