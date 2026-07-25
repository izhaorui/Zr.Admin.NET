using Xunit;

namespace ZR.Tests
{
    /// <summary>
    /// 工作流测试集合。所有工作流测试类以 [Collection("WfTests")] 标记后，
    /// 集合内用例顺序执行，避免与程序集级静态字段 DbScoped.SugarScope、
    /// 以及共享的 SQLite 内存库单连接产生并发冲突。
    /// </summary>
    [CollectionDefinition("WfTests")]
    public class WfTestsCollection : ICollectionFixture<WfTestDb>
    {
    }
}
