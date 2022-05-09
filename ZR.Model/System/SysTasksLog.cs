using OfficeOpenXml.Attributes;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System
{
    /// <summary>
    /// 任务日志
    /// </summary>
    [SugarTable("sys_tasks_log")]
    [Tenant("0")]
    public class SysTasksLog
    {
        /// <summary>
        /// 日志Id
        /// </summary>
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long JobLogId { get; set; }
        /// <summary>
        /// 任务Id
        /// </summary>
        public string JobId { get; set; }
        public string JobName { get; set; }
        public string JobGroup { get; set; }
        /// <summary>
        /// 执行状态（0正常 1失败）
        /// </summary>
        public string Status { get; set; }
        /// <summary>
        /// 异常
        /// </summary>
        public string Exception { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string JobMessage { get; set; }
        /// <summary>
        /// 调用目标字符串
        /// </summary>
        public string InvokeTarget { get; set; }

        [EpplusTableColumn(NumberFormat = "yyyy-MM-dd HH:mm:ss")]
        public DateTime CreateTime { get; set; }
        /// <summary>
        /// 执行用时，毫秒
        /// </summary>
        //[SqlSugar.SugarColumn(IsIgnore = true)]
        public double Elapsed { get; set; }
    }
}
