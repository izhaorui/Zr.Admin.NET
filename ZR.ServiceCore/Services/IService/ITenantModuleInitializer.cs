namespace ZR.ServiceCore.Services
{
	/// <summary>
	/// 租户模块数据库初始化扩展点。
	/// 各业务模块（如商城、内容等）实现此接口，在租户初始化时自动创建本模块所需的业务表。
	/// </summary>
	public interface ITenantModuleInitializer
	{
		/// <summary>
		/// 模块名称，用于生命周期步骤展示。
		/// </summary>
		string ModuleName { get; }

		/// <summary>
		/// 为指定租户初始化模块表结构。
		/// </summary>
		/// <param name="tenantId">租户标识（对应 dbConfigs 中的 ConfigId）</param>
		/// <returns>初始化结果摘要</returns>
		string InitializeTenant(string tenantId);

		/// <summary>
		/// 非SaaS模式的模块表初始化（仅在 CLI --initdb 触发全量初始化时被调用）。
		/// 模块自行判断环境条件，并通过 [Tenant("XxxDb")] 的配置 key 拿到目标 ConfigId。
		/// </summary>
		void InitializeNonSaaS();
	}
}
