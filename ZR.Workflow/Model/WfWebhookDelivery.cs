using System;
using ZR.Model;

namespace ZR.Workflow.Model
{
    /// <summary>
    /// Webhook 投递状态机：
    /// Pending(0) → Processing(1) → Sent(2)  （投递成功）
    ///                    ↘ Pending(0) → 超过 MaxRetry → Dead(3)  （投递失败死信）
    /// Processing 由多实例投递器的原子抢占进入，配合 LockUntil 防重复投递与崩溃恢复。
    /// </summary>
    public enum WfWebhookDeliveryStatus
    {
        /// <summary>待投递（含失败重试回到此态）</summary>
        Pending = 0,
        /// <summary>投递中（已被某 Worker 原子抢占，Processing=1 时与此态对应）</summary>
        Processing = 1,
        /// <summary>投递成功</summary>
        Sent = 2,
        /// <summary>超过最大重试次数，进入死信，不再自动投递</summary>
        Dead = 3
    }

    /// <summary>
    /// 工作流 Webhook 投递记录（Outbox 事务发件箱）。
    /// 节点事件发生时，与业务变更在同一事务内插入一条 Pending 记录；
    /// 后由独立 Quartz 投递器（Job_WfWebhookRetry）扫描并投递，失败指数退避重试、超限死信。
    /// </summary>
    [SugarTable("wf_webhook_delivery")]
    [Tenant("0")]
    public class WfWebhookDelivery : SysBase
    {
        /// <summary>投递记录Id（主键）</summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long DeliveryId { get; set; }

        /// <summary>幂等事件Id，如 evt_20260813xxxx_随机串；投递时随请求携带，供接收方去重</summary>
        [SugarColumn(Length = 64, IsNullable = false)]
        public string EventId { get; set; }

        /// <summary>关联 Webhook 配置Id（WfWebhook.WebhookId），用于多 Webhook/配置追踪</summary>
        public long WebhookId { get; set; }

        /// <summary>Webhook 名称快照（来自 WfWebhook.Name）</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string HookName { get; set; }

        /// <summary>回调地址快照（来自 WfWebhook.Url）</summary>
        [SugarColumn(Length = 500, IsNullable = false)]
        public string HookUrl { get; set; }

        /// <summary>流程实例Id</summary>
        public long InstanceId { get; set; }

        /// <summary>节点Id</summary>
        public long NodeId { get; set; }

        /// <summary>节点名称快照</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string NodeName { get; set; }

        /// <summary>事件类型：node.enter / node.leave</summary>
        [SugarColumn(Length = 32, IsNullable = false)]
        public string EventType { get; set; }

        /// <summary>投递 payload（序列化 JSON 快照，含流程/节点/表单信息）</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string Payload { get; set; }

        /// <summary>投递状态：0=Pending 1=Processing 2=Sent 3=Dead</summary>
        [SugarColumn(IsNullable = false, DefaultValue = "0")]
        public int Status { get; set; } = 0;

        /// <summary>是否正在投递（发送中），防多实例重复投递；与 Status=Processing 对应</summary>
        [SugarColumn(IsNullable = false, DefaultValue = "0")]
        public int Processing { get; set; } = 0;

        /// <summary>已重试次数</summary>
        [SugarColumn(IsNullable = false, DefaultValue = "0")]
        public int RetryCount { get; set; } = 0;

        /// <summary>最大重试次数（达上限仍未成功 → Dead）</summary>
        [SugarColumn(IsNullable = false, DefaultValue = "5")]
        public int MaxRetry { get; set; } = 5;

        /// <summary>最近一次错误信息</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string LastError { get; set; }

        /// <summary>最近一次尝试时间</summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? LastAttemptTime { get; set; }

        /// <summary>最近一次 HTTP 状态码（成功 200；异常置 null）</summary>
        [SugarColumn(IsNullable = true)]
        public int? LastHttpStatusCode { get; set; }

        /// <summary>锁过期时间：Worker 抢占后置 now+有效期，崩溃后过期可由其它 Worker 重新抢占</summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? LockUntil { get; set; }

        /// <summary>下次可投递时间（退避用）；为空表示立即</summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? NextRetryTime { get; set; }

        /// <summary>投递成功时间</summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? SentTime { get; set; }
    }
}
