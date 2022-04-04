using System;
using System.Collections.Generic;
using SqlSugar;
using OfficeOpenXml.Attributes;

namespace ZR.Model.Models
{
    /// <summary>
    /// 演示，数据实体对象
    ///
    /// @author zz
    /// @date 2022-03-31
    /// </summary>
    [SugarTable("gen_demo")]
    public class GenDemo
    {
        /// <summary>
        /// 描述 : id
        /// 空值 : false  
        /// </summary>
        [EpplusTableColumn(Header = "id")]
        [SqlSugar.SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>
        /// 描述 : 名称
        /// 空值 : false  
        /// </summary>
        [EpplusTableColumn(Header = "名称")]
        public string Name { get; set; }

        /// <summary>
        /// 描述 : 图片
        /// 空值 : true  
        /// </summary>
        [EpplusTableColumn(Header = "图片")]
        public string Icon { get; set; }

        /// <summary>
        /// 描述 : 显示状态
        /// 空值 : false  
        /// </summary>
        [EpplusTableColumn(Header = "显示状态")]
        public int ShowStatus { get; set; }

        /// <summary>
        /// 描述 : 添加时间
        /// 空值 : true  
        /// </summary>
        [EpplusTableColumn(Header = "添加时间", NumberFormat = "yyyy-MM-dd HH:mm:ss")]
        public DateTime? AddTime { get; set; }

        /// <summary>
        /// 描述 : 用户性别
        /// 空值 : true  
        /// </summary>
        [EpplusTableColumn(Header = "用户性别")]
        public int? Sex { get; set; }

        /// <summary>
        /// 描述 : 排序
        /// 空值 : true  
        /// </summary>
        [EpplusTableColumn(Header = "排序")]
        public int? Sort { get; set; }

        /// <summary>
        /// 描述 : 备注
        /// 空值 : true  
        /// </summary>
        [EpplusTableColumn(Header = "备注")]
        public string Remark { get; set; }

        /// <summary>
        /// 描述 : 开始时间
        /// 空值 : true  
        /// </summary>
        [EpplusTableColumn(Header = "开始时间", NumberFormat = "yyyy-MM-dd HH:mm:ss")]
        public DateTime? BeginTime { get; set; }

        /// <summary>
        /// 描述 : 结束时间
        /// 空值 : true  
        /// </summary>
        [EpplusTableColumn(Header = "结束时间", NumberFormat = "yyyy-MM-dd HH:mm:ss")]
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// 描述 : 特征
        /// 空值 : true  
        /// </summary>
        [EpplusTableColumn(Header = "特征")]
        public string Feature { get; set; }

        /// <summary>
        /// 描述 : 父级id
        /// 空值 : true  
        /// </summary>
        [EpplusTableColumn(Header = "父级id")]
        public int? ParentId { get; set; }

    }
}