using System;
using System.Collections.Generic;
using SqlSugar;

namespace ZR.Model.Models
{
    /// <summary>
    /// 代码生成演示，数据实体对象
    ///
    /// @author zr
    /// @date 2021-12-01
    /// </summary>
    [SugarTable("gen_demo")]
    [Tenant("0")]
    public class Gendemo
    {
        /// <summary>
        /// 描述 : 自增id
        /// 空值 : false  
        /// </summary>
        [SqlSugar.SugarColumn(IsPrimaryKey = false, IsIdentity = true)]
        public int Id { get; set; }
        /// <summary>
        /// 描述 : 名称
        /// 空值 : false  
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 描述 : 图片
        /// 空值 : true  
        /// </summary>
        public string Icon { get; set; }
        /// <summary>
        /// 描述 : 显示状态
        /// 空值 : false  
        /// </summary>
        public int ShowStatus { get; set; }
        /// <summary>
        /// 描述 : 添加时间
        /// 空值 : true  
        /// </summary>
        public DateTime? AddTime { get; set; }
        /// <summary>
        /// 描述 : 用户性别
        /// 空值 : true  
        /// </summary>
        public int? Sex { get; set; }
        /// <summary>
        /// 描述 : 排序
        /// 空值 : true  
        /// </summary>
        public int? Sort { get; set; }
        /// <summary>
        /// 描述 : 开始时间
        /// 空值 : true  
        /// </summary>
        public DateTime? BeginTime { get; set; }
        /// <summary>
        /// 描述 : 结束时间
        /// 空值 : true  
        /// </summary>
        public DateTime? EndTime { get; set; }
        /// <summary>
        /// 描述 : 备注
        /// 空值 : true  
        /// </summary>
        public string Remark { get; set; }
    }
}