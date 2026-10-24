using Infrastructure.Attribute;
using System.Collections.Concurrent;

namespace ZR.ServiceCore.Email
{
    /// <summary>
    /// 邮件发送内存队列（进程内，轻量）。
    /// 只承载「记录 Id」：记录已先落库（email_log.Status=待发送），进程重启后由
    /// <see cref="EmailDispatchService"/> 的启动恢复扫描重新入队，因此队列本身不需要持久化。
    /// </summary>
    public interface IEmailQueue
    {
        /// <summary>
        /// 尝试入队。返回 false 表示「队列已满」或「该记录已在队列中/正在发送」，
        /// 调用方可据此避免重复入队（补偿扫描不应反复塞入未消费完的记录）。
        /// </summary>
        bool TryEnqueue(long logId);

        /// <summary>非阻塞读取一条（用于成批消费），队列为空返回 false</summary>
        bool TryRead(out long logId);

        /// <summary>标记记录处理完毕，从「已入队」集合移除，允许后续重试/补偿再次入队</summary>
        void MarkCompleted(long logId);

        /// <summary>阻塞消费队列</summary>
        IAsyncEnumerable<long> ReadAllAsync(CancellationToken cancellationToken);
    }

    /// <summary>基于 Channel 的有界内存队列，多生产者单消费者；额外维护「在途集合」用于入队去重。</summary>
    [AppService(ServiceType = typeof(IEmailQueue), ServiceLifetime = LifeTime.Singleton)]
    public class EmailQueue : IEmailQueue
    {
        /// <summary>队列容量。积压超过该值说明 SMTP 严重异常或遭滥用，交给数据库扫描兜底。</summary>
        private const int Capacity = 10000;

        private readonly System.Threading.Channels.Channel<long> channel =
            System.Threading.Channels.Channel.CreateBounded<long>(new System.Threading.Channels.BoundedChannelOptions(Capacity)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });

        /// <summary>
        /// 「已入队或处理中」的记录 Id。
        /// 用途：补偿扫描每分钟都会查到同一批待发送记录，若不去重会反复 TryEnqueue 造成无效循环；
        /// 处理结束（成功/失败）后由 MarkCompleted 移除，失败重试由调度器重新入队。
        /// </summary>
        private readonly ConcurrentDictionary<long, byte> inFlight = new();

        public bool TryEnqueue(long logId)
        {
            if (!inFlight.TryAdd(logId, 0))
            {
                return false;
            }

            if (channel.Writer.TryWrite(logId))
            {
                return true;
            }

            // 队列已满：回滚在途标记，交给下一轮补偿扫描
            inFlight.TryRemove(logId, out _);
            return false;
        }

        public bool TryRead(out long logId)
        {
            return channel.Reader.TryRead(out logId);
        }

        public void MarkCompleted(long logId)
        {
            inFlight.TryRemove(logId, out _);
        }

        public IAsyncEnumerable<long> ReadAllAsync(CancellationToken cancellationToken)
        {
            return channel.Reader.ReadAllAsync(cancellationToken);
        }
    }
}
