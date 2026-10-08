using ZR.Model.System;

namespace ZR.Model.AI
{
    /// <summary>
    /// AI 模型单价。金额按每百万 Token 计，调用流水保存命中价格的快照。
    /// 平台治理统一配置，固定存放于系统库（ConfigId=0），跨租户共享。
    /// <para>
    /// 路由说明：多租户关闭时靠 [Tenant("0")] 固定系统库；多租户开启时由 IMainDbEntity 走主库。
    /// 二者须同时存在，缺 [Tenant] 会导致多租户关闭时被路由到 MainDb 而找不到表。
    /// </para>
    /// </summary>
    [SugarTable("ai_model_price", "AI模型单价")]
    [Tenant("0")]
    [SugarIndex("uk_ai_model_price", nameof(Provider), OrderByType.Asc, true)]
    [SugarIndex("uk_ai_model_price", nameof(Model), OrderByType.Asc, true)]
    public class AiModelPrice : SysBase, IMainDbEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(Length = 32)]
        public string Provider { get; set; }

        [SugarColumn(Length = 100)]
        public string Model { get; set; }

        [SugarColumn(ColumnDataType = "decimal(18,6)", DefaultValue = "0")]
        public decimal InputPricePerMillion { get; set; }

        [SugarColumn(ColumnDataType = "decimal(18,6)", DefaultValue = "0")]
        public decimal OutputPricePerMillion { get; set; }

        [SugarColumn(Length = 8, DefaultValue = "CNY")]
        public string Currency { get; set; } = "CNY";

        /// <summary>0 正常，1 停用。</summary>
        [SugarColumn(DefaultValue = "0")]
        public int Status { get; set; }
    }
}
