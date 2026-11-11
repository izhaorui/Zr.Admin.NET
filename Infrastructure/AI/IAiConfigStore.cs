namespace Infrastructure.AI
{
    /// <summary>
    /// AI 配置段的数据库覆盖存储（平台级）。
    /// <para>
    /// 只负责“读取”，写入由管理端服务负责（需要校验、加密、版本与审计）。
    /// 无实现或该段无配置时返回 null，调用方回退到配置文件，保证不建表也能启动。
    /// </para>
    /// </summary>
    public interface IAiConfigStore
    {
        /// <summary>读取指定段在当前环境下的配置 JSON（已解密）；无配置或读取失败返回 null（宽松模式，调用方回退文件）。</summary>
        string GetSectionJson(string section);

        /// <summary>
        /// 读取指定段在当前环境下的配置 JSON（已解密）。
        /// 返回 false 且 <paramref name="error"/> 非空表示读取过程失败（表未建/库抖动/密文无法解密）；
        /// 返回 true 且 <paramref name="json"/> 为 null 表示“读取成功但没有该段配置”。
        /// 配置源=db 需要据此做失败关闭，不能把异常当成“没有配置”而静默回退文件。
        /// </summary>
        bool TryGetSectionJson(string section, out string json, out string error);
    }
}
