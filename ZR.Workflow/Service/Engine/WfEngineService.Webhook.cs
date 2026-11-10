namespace ZR.Workflow.Service
{
    public partial class WfEngineService
    {
        #region 节点事件钩子（Webhook）

        /// <summary>
        /// 节点进入/离开事件钩子：Outbox 事务发件箱。
        /// 按节点关联的 Webhook 配置（EnterWebhookId / LeaveWebhookId）查询启用的端点，
        /// 在本方法被调用的"业务事务体内"插入一条 Pending 投递记录（含 EventId 幂等键、Payload 快照），
        /// 与流程推进原子落库；投递由独立定时任务 RetryWebhookDeliveries 负责。失败不阻断流转。
        /// </summary>
        /// <param name="instance">流程实例（提供 InstanceId/Title/FormContent）</param>
        /// <param name="node">触发节点（提供 NodeId/NodeName/WebhookId 引用）</param>
        /// <param name="eventType">enter / leave（映射为 node.enter / node.leave）</param>
        /// <param name="formValues">表单字段值（快照进 payload，便于外部系统取值）</param>
        private void QueueNodeHook(WfFlowInstance instance, WfFlowNode node, string eventType, Dictionary<string, string> formValues)
        {
            var webhookId = eventType == "enter" ? node.EnterWebhookId : node.LeaveWebhookId;
            if (webhookId == null || webhookId <= 0) return;

            var cfg = _webhookService.GetFirst(it => it.WebhookId == webhookId && it.Enabled == 1);
            if (cfg == null) return; // 配置不存在或已停用，不投递

            var eventTypeNorm = eventType == "enter" ? "node.enter" : "node.leave";
            var eventId = $"evt_{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
            var payload = new
            {
                eventId,
                eventType = eventTypeNorm,
                webhookId = cfg.WebhookId,
                instanceId = instance.InstanceId,
                flowId = instance.FlowId,
                flowName = instance.FlowName,
                title = instance.Title,
                businessKey = instance.BusinessKey,
                nodeId = node.NodeId,
                nodeName = node.NodeName,
                nodeType = node.NodeType,
                formContent = instance.FormContent,
                formValues,
                time = DateTime.Now
            };

            var delivery = new WfWebhookDelivery
            {
                EventId = eventId,
                WebhookId = cfg.WebhookId,
                HookName = cfg.Name,
                HookUrl = cfg.Url,
                InstanceId = instance.InstanceId,
                NodeId = node.NodeId,
                NodeName = node.NodeName,
                EventType = eventTypeNorm,
                Payload = JsonConvert.SerializeObject(payload),
                Status = (int)WfWebhookDeliveryStatus.Pending,
                Processing = 0,
                RetryCount = 0,
                MaxRetry = 5,
                Create_time = DateTime.Now
            };
            Context.Insertable(delivery).ExecuteCommand();
            logger.Info($"[节点钩子:{eventTypeNorm}] 实例{instance.InstanceId} 节点{node.NodeName}({node.NodeId}) 已登记 Outbox 投递 EventId={eventId} Webhook={cfg.Name}");
        }

        /// <summary>
        /// 投递 Outbox 中待发 / 到期可重试的 Webhook 记录（由 Job_WfWebhookRetry 定时调用）。
        /// 多实例安全：用单条原子 UPDATE 抢占（WHERE Status=Pending AND LockUntil 过期），抢到才投递，
        /// 避免多个 Worker 重复投递同一条；Worker 崩溃后 LockUntil 过期可被其它实例重新抢占。
        /// 全程 try/catch，绝不向调用方抛异常，不阻断主流程。
        /// </summary>
        public void RetryWebhookDeliveries()
        {
            var now = DateTime.Now;
            var due = Context.Queryable<WfWebhookDelivery>()
                .Where(it => ((it.Status == (int)WfWebhookDeliveryStatus.Pending)
                        && (it.NextRetryTime == null || it.NextRetryTime <= now))
                    || ((it.Status == (int)WfWebhookDeliveryStatus.Processing) && it.LockUntil < now))
                .OrderBy(it => it.Create_time)
                .Take(200)
                .ToList();

            foreach (var d in due)
            {
                long id = d.DeliveryId;
                // ① 原子抢占：CAS 把 Pending 改为 Processing，并置 LockUntil 防重复
                var claimed = Context.Updateable<WfWebhookDelivery>()
                    .SetColumns(it => new WfWebhookDelivery
                    {
                        Status = (int)WfWebhookDeliveryStatus.Processing,
                        Processing = 1,
                        LockUntil = DateTime.Now.AddSeconds(60)
                    })
                    .Where(it => it.DeliveryId == id
                        && (it.Status == (int)WfWebhookDeliveryStatus.Pending
                            || it.Status == (int)WfWebhookDeliveryStatus.Processing)
                        && (it.LockUntil == null || it.LockUntil < DateTime.Now))
                    .ExecuteCommand();
                if (claimed <= 0) continue; // 被其它 Worker 抢占 / 已锁定中，跳过

                try
                {
                    // 投递到 Webhook 端点（受保护虚拟方法，便于测试注入成功/失败/计数）
                    SendWebhook(d.HookUrl, d.Payload ?? "{}");
                    // 抢占到 → 投递成功：置 Sent
                    Context.Updateable<WfWebhookDelivery>()
                        .SetColumns(it => new WfWebhookDelivery
                        {
                            Status = (int)WfWebhookDeliveryStatus.Sent,
                            Processing = 0,
                            LockUntil = null,
                            LastAttemptTime = DateTime.Now,
                            LastHttpStatusCode = 200,
                            LastError = null,
                            SentTime = DateTime.Now
                        })
                        .Where(it => it.DeliveryId == id)
                        .ExecuteCommand();
                    logger.Info($"[Webhook投递] EventId={d.EventId} Webhook={d.HookName} 投递成功");
                }
                catch (Exception ex)
                {
                    // 失败：回 Pending，RetryCount++，指数退避算 NextRetryTime，超限 → Dead
                    var newCount = d.RetryCount + 1;
                    int status;
                    DateTime? next = null;
                    if (newCount >= d.MaxRetry)
                    {
                        status = (int)WfWebhookDeliveryStatus.Dead;
                    }
                    else
                    {
                        status = (int)WfWebhookDeliveryStatus.Pending;
                        // 指数退避：2^RetryCount 分钟（1→2m,2→4m,3→8m,4→16m）
                        next = DateTime.Now.AddMinutes(Math.Pow(2, newCount));
                    }
                    Context.Updateable<WfWebhookDelivery>()
                        .SetColumns(it => new WfWebhookDelivery
                        {
                            Status = status,
                            Processing = 0,
                            LockUntil = null,
                            RetryCount = newCount,
                            LastAttemptTime = DateTime.Now,
                            LastHttpStatusCode = null,
                            LastError = ex.Message,
                            NextRetryTime = next
                        })
                        .Where(it => it.DeliveryId == id)
                        .ExecuteCommand();
                    logger.Error($"[Webhook投递] EventId={d.EventId} Webhook={d.HookName} 第{newCount}次失败：{ex.Message} → {(status == (int)WfWebhookDeliveryStatus.Dead ? "Dead" : "Pending")}");
                }
            }
        }

        /// <summary>
        /// 向 Webhook 端点投递 payload（POST JSON）。抽出为受保护虚拟方法，便于单元测试通过子类重写注入成功/失败/计数。
        /// 默认实现走框架 HttpHelper；投递异常直接向上抛，由 RetryWebhookDeliveries 统一处理为退避/死信。
        /// </summary>
        protected virtual void SendWebhook(string url, string body)
        {
            HttpHelper.HttpPostAsync(url, body, "application/json").GetAwaiter().GetResult();
        }

        #endregion
    }
}
