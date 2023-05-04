using MiniExcelLibs.Attributes;
using SqlSugar;
using System;
using System.ComponentModel;

namespace ZR.Model.System
{
    [SugarTable("sys_oper_log")]
    [Tenant("0")]
    public class SysOperLog
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long OperId { get; set; }
        /// <summary>
        /// 操作模块
        /// </summary>
        [DisplayName("操作模块")]
        public string Title { get; set; }

        /// <summary>
        /// 业务类型（0其它 1新增 2修改 3删除 4=授权,5=导出,6=导入,7=强退,8=生成代码,9=清空数据）
        /// </summary>
        [DisplayName("业务类型")]
        public int BusinessType { get; set; }

        /// <summary>
        /// 业务类型数组
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        [ExcelIgnore]
        public int[] BusinessTypes { get; set; }

        /// <summary>
        /// 请求方法
        /// </summary>
        [DisplayName("请求方法")]
        public string Method { get; set; }

        /// <summary>
        /// 请求方式
        /// </summary>
        [DisplayName("请求方式")]
        public string RequestMethod { get; set; }

        /// <summary>
        /// 操作类别（0其它 1后台用户 2手机端用户）
        /// </summary>
        //@Excel(name = "操作类别", readConverterExp = "0=其它,1=后台用户,2=手机端用户")
        [DisplayName("操作类别")]
        public int OperatorType { get; set; }

        /// <summary>
        /// 操作人员
        /// </summary>
        [DisplayName("操作人员")]
        public string OperName { get; set; }

        /// <summary>
        /// 请求url
        /// </summary>
        [DisplayName("请求地址")]
        public string OperUrl { get; set; }

        /// <summary>
        /// 操作地址
        /// </summary>
        [DisplayName("操作地址")]
        public string OperIp { get; set; }

        /// <summary>
        /// 操作地点
        /// </summary>
        [DisplayName("操作地点")]
        public string OperLocation { get; set; }

        /// <summary>
        /// 请求参数
        /// </summary>
        [DisplayName("请求参数")]
        public string OperParam { get; set; }

        /// <summary>
        /// 返回参数
        /// </summary>
        [DisplayName("返回结果")]
        public string JsonResult { get; set; }

        /// <summary>
        /// 操作状态（0正常 1异常）
        /// </summary>
        [DisplayName("状态")]
        public int Status { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        [DisplayName("错误消息")]
        public string ErrorMsg { get; set; }

        /// <summary>
        /// 操作时间
        /// </summary>
        [DisplayName("操作时间")]
        public DateTime? OperTime { get; set; }
        /// <summary>
        /// 操作用时
        /// </summary>
        [DisplayName("操作用时")]
        public long Elapsed { get; set; }
    }
}
