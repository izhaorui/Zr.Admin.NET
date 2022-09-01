using OfficeOpenXml.Attributes;
using SqlSugar;
using System;

namespace ZR.Model.System
{
    [SugarTable("sys_oper_log")]
    [Tenant("0")]
    public class SysOperLog
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long OperId { get; set; }
        /** 操作模块 */
        [EpplusTableColumn(Header = "操作模块")]
        public string Title { get; set; }

        /** 业务类型（0其它 1新增 2修改 3删除） */
        //@Excel(name = "业务类型", readConverterExp = "0=其它,1=新增,2=修改,3=删除,4=授权,5=导出,6=导入,7=强退,8=生成代码,9=清空数据")
        [EpplusTableColumn(Header = "业务类型")] 
        public int BusinessType { get; set; }

        /** 业务类型数组 */
        [SugarColumn(IsIgnore = true)]
        [EpplusIgnore]
        public int[] BusinessTypes { get; set; }

        /** 请求方法 */
        [EpplusTableColumn(Header = "请求方法")]
        public string Method { get; set; }

        /** 请求方式 */
        [EpplusTableColumn(Header = "请求方式")]
        public string RequestMethod { get; set; }

        /** 操作类别（0其它 1后台用户 2手机端用户） */
        //@Excel(name = "操作类别", readConverterExp = "0=其它,1=后台用户,2=手机端用户")
        [EpplusTableColumn(Header = "操作类别")]
        public int OperatorType { get; set; }

        /** 操作人员 */
        [EpplusTableColumn(Header = "操作人员")]
        public string OperName { get; set; }

        /** 部门名称 */
        [EpplusTableColumn(Header = "部门名称")]
        public string DeptName { get; set; }

        /** 请求url */
        [EpplusTableColumn(Header = "请求地址")]
        public string OperUrl { get; set; }

        /** 操作地址 */
        [EpplusTableColumn(Header = "操作地址")]
        public string OperIp { get; set; }

        /** 操作地点 */
        [EpplusTableColumn(Header = "操作地点")]
        public string OperLocation { get; set; }

        /** 请求参数 */
        [EpplusTableColumn(Header = "请求参数")]
        public string OperParam { get; set; }

        /** 返回参数 */
        [EpplusTableColumn(Header = "返回结果")]
        public string JsonResult { get; set; }

        /** 操作状态（0正常 1异常） */
        [EpplusTableColumn(Header = "状态")]
        public int Status { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        [EpplusTableColumn(Header = "错误消息")]
        public string ErrorMsg { get; set; }

        /// <summary>
        /// 操作时间
        /// </summary>
        [EpplusTableColumn(Header = "操作时间", NumberFormat = "yyyy-MM-dd HH:mm:ss")]
        public DateTime? OperTime { get; set; }
        /// <summary>
        /// 操作用时
        /// </summary>
        public long Elapsed { get; set; }
    }
}
