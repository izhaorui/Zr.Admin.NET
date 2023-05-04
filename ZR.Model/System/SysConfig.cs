using SqlSugar;

namespace ZR.Model.System
{
    /// <summary>
    /// 参数配置，数据实体对象
    ///
    /// @author mr.zhao
    /// @date 2021-09-29
    /// </summary>
    [SugarTable("sys_config")]
    [Tenant("0")]
    public class SysConfig : SysBase
    {
        /// <summary>
        /// 配置id
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int ConfigId { get; set; }
        /// <summary>
        /// 参数名称
        /// </summary>
        public string ConfigName { get; set; }
        /// <summary>
        /// 参数键名
        /// </summary>
        public string ConfigKey { get; set; }
        /// <summary>
        /// 参数键值
        /// </summary>
        public string ConfigValue { get; set; }
        /// <summary>
        /// 系统内置（Y是 N否）
        /// </summary>
        public string ConfigType { get; set; }

    }
}
