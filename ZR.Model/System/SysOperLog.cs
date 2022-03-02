using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;
using OfficeOpenXml.Attributes;

namespace ZR.Model.System
{
    [SugarTable("sys_oper_log")]
    [Tenant("0")]
    public class SysOperLog
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long OperId { get; set; }
        /** 操作模块 */
        //@Excel(name = "操作模块")
        public string title { get; set; }

        /** 业务类型（0其它 1新增 2修改 3删除） */
        //@Excel(name = "业务类型", readConverterExp = "0=其它,1=新增,2=修改,3=删除,4=授权,5=导出,6=导入,7=强退,8=生成代码,9=清空数据")
        public int businessType { get; set; }

        /** 业务类型数组 */
        [SugarColumn(IsIgnore = true)]
        public int[] businessTypes { get; set; }

        /** 请求方法 */
        //@Excel(name = "请求方法")
        public string method { get; set; }

        /** 请求方式 */
        //@Excel(name = "请求方式")
        public string requestMethod { get; set; }

        /** 操作类别（0其它 1后台用户 2手机端用户） */
        //@Excel(name = "操作类别", readConverterExp = "0=其它,1=后台用户,2=手机端用户")
        public int operatorType { get; set; }

        /** 操作人员 */
        //@Excel(name = "操作人员")
        public string operName { get; set; }

        /** 部门名称 */
        //@Excel(name = "部门名称")
        public string deptName { get; set; }

        /** 请求url */
        //@Excel(name = "请求地址")
        public string operUrl { get; set; }

        /** 操作地址 */
        //@Excel(name = "操作地址")
        public string operIp { get; set; }

        /** 操作地点 */
        //@Excel(name = "操作地点")
        public string operLocation { get; set; }

        /** 请求参数 */
        //@Excel(name = "请求参数")
        public string operParam { get; set; }

        /** 返回参数 */
        //@Excel(name = "返回参数")
        public string jsonResult { get; set; }

        /** 操作状态（0正常 1异常） */
        //@Excel(name = "状态", readConverterExp = "0=正常,1=异常")
        public int status { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        [EpplusTableColumn(Header = "错误消息")]
        public string errorMsg { get; set; }

        /// <summary>
        /// 操作时间
        /// </summary>
        [EpplusTableColumn(Header = "操作时间", NumberFormat = "yyyy-MM-dd HH:mm:ss")]
        public DateTime? operTime { get; set; }
        /// <summary>
        /// 操作用时
        /// </summary>
        public long Elapsed { get; set; }
    }
}
