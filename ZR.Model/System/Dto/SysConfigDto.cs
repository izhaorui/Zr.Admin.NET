using System;

namespace ZR.Model.Dto
{
    /// <summary>
    /// 参数配置输入对象模型
    /// </summary>
    public class SysConfigDto
    {
        public int ConfigId { get; set; }
        public string ConfigName { get; set; }
        public string ConfigKey { get; set; }
        public string ConfigValue { get; set; }
        public string ConfigType { get; set; }
        public string Remark { get; set; }
    }

    /// <summary>
    /// 参数配置查询对象模型
    /// </summary>
    public class SysConfigQueryDto: PagerInfo 
    {
        public string ConfigName { get; set; }
        public string ConfigKey { get; set; }
        public string ConfigValue { get; set; }
        public string ConfigType { get; set; }
        public DateTime? BeginTime { get; set; }
        public DateTime? EndTime { get; set; }
    }
}
