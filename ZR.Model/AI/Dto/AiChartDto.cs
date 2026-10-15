namespace ZR.Model.AI.Dto
{
    /// <summary>图表字段定义（给模型选 xField / series.field）</summary>
    public class AiChartFieldDef
    {
        public string Field { get; set; }
        public string Label { get; set; }
        /// <summary>category | number</summary>
        public string Kind { get; set; }
    }

    /// <summary>图表数据集的一个统计维度（同一份数据的不同切片）</summary>
    public class AiChartDimension
    {
        /// <summary>维度 Key，模型传 dimension 时使用；同时作为该类目字段名</summary>
        public string Key { get; set; }

        /// <summary>维度中文名，用于工具描述与图表标题</summary>
        public string Label { get; set; }

        /// <summary>什么问题时该选这个维度，帮助模型判断</summary>
        public string Hint { get; set; }
    }

    /// <summary>单条系列配置（AI 产出，后端校验）</summary>
    public class AiChartSeriesSpec
    {
        public string Name { get; set; }
        public string Field { get; set; }
    }

    /// <summary>受约束的图表配置，禁止完整 ECharts option / formatter</summary>
    public class AiChartSpecDto
    {
        public string DatasetId { get; set; }
        /// <summary>line | bar | pie</summary>
        public string Type { get; set; }
        public string Title { get; set; }
        public string XField { get; set; }
        public List<AiChartSeriesSpec> Series { get; set; } = new();
    }

    /// <summary>工具查数结果：schema + 聚合行（数值以后端为准）</summary>
    public class AiChartQueryResult
    {
        public string DatasetId { get; set; }
        public string Title { get; set; }
        public string Grain { get; set; }
        public string TimeRange { get; set; }
        public string SuggestedType { get; set; }
        public string[] AllowedTypes { get; set; }
        public List<AiChartFieldDef> Fields { get; set; } = new();
        public List<Dictionary<string, object>> Rows { get; set; } = new();
    }

    /// <summary>下发给前端渲染的图表（配置 + 后端数据）</summary>
    public class AiChartViewDto
    {
        public string DatasetId { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string XField { get; set; }
        public List<AiChartSeriesSpec> Series { get; set; } = new();
        public List<Dictionary<string, object>> Rows { get; set; } = new();

        /// <summary>
        /// 字段定义（含中文标签），供前端导出 Excel 时生成中文表头。
        /// 附加字段，旧前端忽略即可，不影响既有渲染。
        /// </summary>
        public List<AiChartFieldDef> Fields { get; set; } = new();
    }

    /// <summary>图表导出的一列：Field=数据字段名，Label=表头显示名</summary>
    public class AiChartExportColumn
    {
        public string Field { get; set; }
        public string Label { get; set; }
    }

    /// <summary>
    /// AI 助手图表导出请求：把助手已渲染的图表数据（后端聚合结果，见 <see cref="AiChartViewDto"/>）
    /// 导出为 Excel。字段与行由前端从 <c>done.Charts</c> 展开后回传，服务端只负责落 Excel，不重新查数。
    /// </summary>
    public class AiChartExportInput
    {
        /// <summary>图表标题，同时作为 Excel 文件名（空时用默认名）</summary>
        public string Title { get; set; }

        /// <summary>列定义（顺序即 Excel 列顺序）</summary>
        public List<AiChartExportColumn> Columns { get; set; } = new();

        /// <summary>数据行，key 为字段名</summary>
        public List<Dictionary<string, object>> Rows { get; set; } = new();
    }
}
