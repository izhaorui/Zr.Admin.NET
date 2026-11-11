using ZR.Model.System;

namespace ZR.Model.AI
{
    /// <summary>
    /// AI 运行时配置覆盖（平台级，固定系统库）。
    /// <para>
    /// 每个 <see cref="Environment"/> + <see cref="Section"/> 一行，<see cref="ConfigJson"/> 为整段配置
    /// （如 AiOptions / AiKnowledge）。后台维护后由 <c>AiOptionsResolver</c> 读取，
    /// 数据库无配置时自动回退 ai.json，保证不建表也能启动。
    /// </para>
    /// <para>
    /// 路由说明与 AiModelPrice 一致：多租户关闭时靠 [Tenant("0")] 固定系统库；
    /// 多租户开启时由 IMainDbEntity 走主库，二者须同时存在。
    /// </para>
    /// </summary>
    [SugarTable("ai_config", "AI运行时配置")]
    [Tenant("0")]
    [SugarIndex("uk_ai_config_env_section", nameof(Environment), OrderByType.Asc, true)]
    [SugarIndex("uk_ai_config_env_section", nameof(Section), OrderByType.Asc, true)]
    public class AiConfig : SysBase, IMainDbEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>环境名（Development / Production）；* 表示所有环境通用。</summary>
        [SugarColumn(Length = 20, DefaultValue = "*")]
        public string Environment { get; set; } = "*";

        /// <summary>配置段名，与配置节名一致（AiOptions / AiKnowledge）。</summary>
        [SugarColumn(Length = 50)]
        public string Section { get; set; }

        /// <summary>
        /// 整段配置 JSON；含密钥时为 DataProtection 密文（enc:v1: 前缀）。
        /// 必须用大文本类型：加密后的配置约 2~3 KB，默认 nvarchar(500) 会直接截断报错。
        /// </summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
        public string ConfigJson { get; set; }

        /// <summary>0 启用；1 停用。</summary>
        [SugarColumn(DefaultValue = "0")]
        public int Status { get; set; }

        /// <summary>每次保存递增，便于审计与排查“当前生效的是哪一版”。</summary>
        [SugarColumn(DefaultValue = "0")]
        public long Version { get; set; }
    }
}
