using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System
{
    /// <summary>
    /// 参数配置，数据实体对象
    ///
    /// @author zhaorui
    /// @date 2021-09-29
    /// </summary>
    [SugarTable("sys_config")]
    [Tenant("0")]
    public class SysConfig: SysBase
    {
        /// <summary>
        /// 描述 :
        /// 空值 :False
        /// </summary>
        [SqlSugar.SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int ConfigId { get; set; }
        /// <summary>
        /// 描述 :
        /// 空值 :True
        /// </summary>
        public string ConfigName { get; set; }
        /// <summary>
        /// 描述 :
        /// 空值 :True
        /// </summary>
        public string ConfigKey { get; set; }
        /// <summary>
        /// 描述 :
        /// 空值 :True
        /// </summary>
        public string ConfigValue { get; set; }
        /// <summary>
        /// 描述 :
        /// 空值 :True
        /// </summary>
        public string ConfigType { get; set; }
        
    }
}
