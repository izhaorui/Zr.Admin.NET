using System;

namespace ZR.Model.System.Dto
{
    public class SysOperLogQueryDto : PagerInfo
    {
        /// <summary>
        /// 操作人员
        /// </summary>
        public string OperName { get; set; }
        /// <summary>
        /// 业务类型 0=其它,1=新增,2=修改,3=删除,4=授权,5=导出,6=导入,7=强退,8=生成代码,9=清空数据
        /// </summary>
        public int? BusinessType { get; set; }
        public int[] BusinessTypes { get; set; }
        /// <summary>
        /// 状态
        /// </summary>
        public int? Status { get; set; }
        /// <summary>
        /// 操作模块
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// 请求参数
        /// </summary>
        public string OperParam { get; set; }
        public DateTime? BeginTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    public class SysOperLogDto : SysBase
    {
        /// <summary>
        /// 页码
        /// </summary>
        public int PageNum { get; set; }
        public int PageSize { get; set; }
        /// <summary>
        /// 操作人员
        /// </summary>
        public string OperName { get; set; }
        /// <summary>
        /// 业务类型 0=其它,1=新增,2=修改,3=删除,4=授权,5=导出,6=导入,7=强退,8=生成代码,9=清空数据
        /// </summary>
        public int BusinessType { get; set; } = -1;
        /// <summary>
        /// 状态
        /// </summary>
        public int Status { get; set; } = -1;
        /// <summary>
        /// 操作模块
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// 请求参数
        /// </summary>
        public string OperParam { get; set; }
    }
}
