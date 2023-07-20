namespace ZR.Model.System
{
    [SugarTable("SqlDiffLog")]
    public class SqlDiffLog
    {
        [SugarColumn(IsPrimaryKey = true)]
        public long PId { get; set; }
        public string TableName { get; set; }
        [SugarColumn(Length = 4000)]
        public object BusinessData { get; set; }
        public string DiffType { get; set; }
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString)]
        public string Sql { get; set; }
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString)]
        public string BeforeData { get; set; }
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString)]
        public string AfterData { get; set; }
        public string UserName { get; set; }
        public DateTime AddTime { get; set; }
    }
}
