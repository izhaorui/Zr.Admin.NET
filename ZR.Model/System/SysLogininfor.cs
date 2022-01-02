//using Dapper.Contrib.Extensions;
using OfficeOpenXml.Attributes;
using SqlSugar;
using System;

namespace ZR.Model.System
{
    /// <summary>
    /// sys_logininfor 表
    /// </summary>
    [SugarTable("sys_logininfor")]
    [Tenant("0")]
    public class SysLogininfor
    {
        //[Key]
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long infoId { get; set; }

        /// <summary>
        /// 用户账号
        /// </summary>
        public string userName { get; set; }

        /// <summary>
        /// 登录状态 0成功 1失败
        /// </summary>
        public string status { get; set; }

        /// <summary>
        /// 登录IP地址
        /// </summary>
        public string ipaddr { get; set; }

        /// <summary>
        /// 登录地点
        /// </summary>
        public string loginLocation { get; set; }

        /// <summary>
        /// 浏览器类型
        /// </summary>
        public string browser { get; set; }

        /** 操作系统 */
        //@Excel(name = "操作系统")
        public string os { get; set; }

        /// <summary>
        /// 提示消息
        /// </summary>
        public string msg { get; set; }

        /// <summary>
        /// 访问时间
        /// </summary>
        [EpplusTableColumn(NumberFormat = "yyyy-MM-dd HH:mm:ss")]
        public DateTime loginTime { get; set; } = DateTime.Now;
        [SugarColumn(IsIgnore = true)]
        public DateTime? BeginTime { get; set; }
        [SugarColumn(IsIgnore = true)]
        public DateTime? EndTime { get; set; }
    }
}
