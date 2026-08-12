using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Infrastructure;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using ZR.ServiceCore.Services;
using ZR.Workflow.Enum;
using ZR.Workflow.Model;
using ZR.Workflow.Service;

namespace ZR.Tests
{
    /// <summary>
    /// 节点事件钩子（Webhook）端到端验证：
    /// 构造带 EnterHookUrl/LeaveHookUrl 的审批节点，发起并审批流程，
    /// 断言引擎在"节点进入"与"节点离开"时各向对应 URL 发一次 POST，且 payload 关键字段正确。
    /// 失败时（网络/超时）不应阻断流转 —— 单独用一例验证。
    /// </summary>
    [Collection("WfTests")]
    public class WfEngineHookTests : IDisposable
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;
        private readonly HttpListener _listener;
        private readonly string _enterUrl;
        private readonly string _leaveUrl;
        private readonly ConcurrentBag<string> _received = new();
        private readonly ManualResetEventSlim _enterSignal = new(false);
        private readonly ManualResetEventSlim _leaveSignal = new(false);

        public WfEngineHookTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _db.EnsureUsers("alice", "zhangsan", "wangwu");

            var port = 8731;
            _enterUrl = $"http://127.0.0.1:{port}/enter/";
            _leaveUrl = $"http://127.0.0.1:{port}/leave/";
            _listener = new HttpListener();
            _listener.Prefixes.Add(_enterUrl);
            _listener.Prefixes.Add(_leaveUrl);
            _listener.Start();
            _listener.BeginGetContext(OnRequest, null);

            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>());
        }

        private void OnRequest(IAsyncResult ar)
        {
            try
            {
                var ctx = _listener.EndGetContext(ar);
                // 继续监听后续请求
                _listener.BeginGetContext(OnRequest, null);

                using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                var body = reader.ReadToEnd();
                _received.Add(body);

                if (ctx.Request.Url.AbsolutePath.EndsWith("/enter/"))
                    _enterSignal.Set();
                else if (ctx.Request.Url.AbsolutePath.EndsWith("/leave/"))
                    _leaveSignal.Set();

                ctx.Response.StatusCode = 200;
                ctx.Response.Close();
            }
            catch
            {
                // 测试结束时 listener 已停止，忽略回调异常
            }
        }

        private long BuildFlowWithHook(out long node1, out long node2)
        {
            var flowId = _db.AddDefinition("HOOK", "钩子流程");
            node1 = _db.AddNode(flowId, "主管审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("zhangsan"), 1,
                enterHookUrl: _enterUrl, leaveHookUrl: _leaveUrl);
            node2 = _db.AddNode(flowId, "总监审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("wangwu"), 2);
            return flowId;
        }

        [Fact]
        public void Hook_审批节点_进入与离开各回调一次且Payload正确()
        {
            var flowId = BuildFlowWithHook(out var node1, out var node2);
            var id = _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "请假1天",
                ApplyUser = "alice",
                ApplyUserId = _db.Uid("alice"),
                FormContent = "{\"reason\":\"病假\",\"days\":\"1\"}",
            });

            // 发起后节点1到达 → enter 钩子应触发
            Assert.True(_enterSignal.Wait(TimeSpan.FromSeconds(5)), "未在 5s 内收到 enter 钩子回调");

            // 审批节点1 → leave 钩子应触发
            var task = _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == id && t.NodeId == node1);
            _engine.Approve(task.TaskId, "同意", _db.Uid("zhangsan"));
            Assert.True(_leaveSignal.Wait(TimeSpan.FromSeconds(5)), "未在 5s 内收到 leave 钩子回调");

            // 校验收到内容：按 payload 的 eventType 字段分别取（ConcurrentBag 不保证顺序）
            Assert.Equal(2, _received.Count);
            var parsed = _received.Select(JObject.Parse).ToList();
            var enterBody = parsed.FirstOrDefault(j => j["eventType"]?.ToString() == "enter");
            var leaveBody = parsed.FirstOrDefault(j => j["eventType"]?.ToString() == "leave");
            Assert.NotNull(enterBody);
            Assert.NotNull(leaveBody);
            AssertPayload(enterBody.ToString(), "enter", id, node1, "主管审批");
            AssertPayload(leaveBody.ToString(), "leave", id, node1, "主管审批");
        }

        [Fact]
        public void Hook_勾子URL不可达_不阻断流转()
        {
            // 指向一个没人监听的端口，FireNodeHook 内部 catch 应吞掉异常，流程照常推进
            var flowId = _db.AddDefinition("HOOKFAIL", "钩子失败流程");
            var badEnter = "http://127.0.0.1:9999/enter/";
            var badLeave = "http://127.0.0.1:9999/leave/";
            var node1 = _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("zhangsan"), 1,
                enterHookUrl: badEnter, leaveHookUrl: badLeave);
            var node2 = _db.AddNode(flowId, "二级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("wangwu"), 2);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });
            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal(node1, saved.CurrentNodeId); // 进入钩子异常不应阻断到达

            var task = _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == id && t.NodeId == node1);
            _engine.Approve(task.TaskId, "同意", _db.Uid("zhangsan"));

            var after = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal(node2, after.CurrentNodeId); // 离开钩子异常不应阻断推进
            Assert.Equal((int)WfInstanceStatus.Approval, after.Status);
        }

        private static void AssertPayload(string body, string expectEventType, long instanceId, long nodeId, string nodeName)
        {
            var json = JObject.Parse(body);
            Assert.Equal(expectEventType, json["eventType"]?.ToString());
            Assert.Equal(instanceId.ToString(), json["instanceId"]?.ToString());
            Assert.Equal(nodeId.ToString(), json["nodeId"]?.ToString());
            Assert.Equal(nodeName, json["nodeName"]?.ToString());
            Assert.NotNull(json["formContent"]);
            Assert.NotNull(json["time"]);
        }

        public void Dispose()
        {
            try { _listener.Stop(); _listener.Close(); } catch { }
            _enterSignal.Dispose();
            _leaveSignal.Dispose();
        }
    }
}
