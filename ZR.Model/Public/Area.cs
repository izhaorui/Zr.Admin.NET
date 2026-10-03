namespace ZR.Model.Public
{
    /// <summary>
    /// 行政区划（省 / 市 / 区县三级字典）—— 主库表 sys_area
    /// </summary>
    /// <remarks>
    /// 全局公共参考数据（不属于任一商城租户），故放主库与 ZR.Model。
    /// 层级只能由 <see cref="Level"/> 判定，不能按 AreaCode 位数反推 —— 东莞等不设区地级市为 9 位街道码。
    /// </remarks>
    [SugarTable("sys_area", "行政区划")]
    // 地区解析按编码定位全靠它，末位 true = 唯一
    [SugarIndex("idx_sys_area_code", nameof(AreaCode), OrderByType.Asc, true)]
    // 后台地区级联按 ParentId 懒加载、灌种按层级回查 Id
    [SugarIndex("idx_sys_area_parent", nameof(ParentId), OrderByType.Asc, false)]
    [Tenant("0")]
    public class Area : IMainDbEntity
    {
        /// <summary>
        /// 主键
        /// </summary>
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long Id { get; set; }

        /// <summary>
        /// 行政编码（GB/T 2260 统计用区划代码）
        /// </summary>
        /// <remarks>全局唯一，建唯一索引 <c>idx_sys_area_code</c>；重复会导致灌种子失败</remarks>
        [SugarColumn(Length = 16)]
        public string AreaCode { get; set; }

        /// <summary>
        /// 名称，如「浙江省」「杭州市」「西湖区」
        /// </summary>
        [SugarColumn(Length = 64)]
        public string Name { get; set; }

        /// <summary>
        /// 父级编码；省级为 <c>null</c>
        /// </summary>
        /// <remarks>
        /// 业务侧的关联依据：灌种、导入导出、跨库对齐都靠它，天然稳定。
        /// </remarks>
        [SugarColumn(Length = 16)]
        public string ParentCode { get; set; }

        /// <summary>
        /// 父级 Id；省级为 0
        /// </summary>
        /// <remarks>
        /// 查询优化用的冗余：后台级联下拉按 parent_id 走索引，避免每次按编码回查。
        /// 与 <see cref="ParentCode"/> 不一致时以后台树查询为准，但灌种保证两者一致。
        /// </remarks>
        public long ParentId { get; set; }

        /// <summary>
        /// 层级：1 省 / 2 市 / 3 区县，见 <see cref="Enum.AreaLevelEnum"/>
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// 排序号（灌种时按 省→市→区 的展开顺序生成，保证同省同市内顺序稳定）
        /// </summary>
        public int SortId { get; set; }
    }
}
