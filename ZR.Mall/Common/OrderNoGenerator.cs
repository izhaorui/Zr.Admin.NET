using System;

namespace ZR.Mall.Common
{
    /// <summary>
    /// 订单号生成器
    /// 采用雪花ID（SnowFlakeSingle）：全局唯一、多实例安全、趋势递增。
    /// 注意：SnowFlakeSingle.WorkId 由 WebApi 启动时初始化（Program.cs）。
    /// </summary>
    public static class OrderNoGenerator
    {
        public static string Generate()
        {
            return SnowFlakeSingle.Instance.NextId().ToString();
        }
    }
}
