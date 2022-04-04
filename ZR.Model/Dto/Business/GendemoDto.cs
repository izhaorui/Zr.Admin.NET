using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ZR.Model.Dto;
using ZR.Model.Models;

namespace ZR.Model.Dto
{
    /// <summary>
    /// 演示输入对象
    /// </summary>
    public class GenDemoDto
    {
        [Required(ErrorMessage = "id不能为空")]
        public int Id { get; set; }
        [Required(ErrorMessage = "名称不能为空")]
        public string Name { get; set; }
        public string Icon { get; set; }
        [Required(ErrorMessage = "显示状态不能为空")]
        public int ShowStatus { get; set; }
        public int? Sex { get; set; }
        public int? Sort { get; set; }
        public string Remark { get; set; }
        public DateTime? BeginTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Feature { get; set; }
    }

    /// <summary>
    /// 演示查询对象
    /// </summary>
    public class GenDemoQueryDto : PagerInfo 
    {
        public int? Id { get; set; }
        public string Name { get; set; }
        public int? ShowStatus { get; set; }
        public DateTime? BeginAddTime { get; set; }
        public DateTime? EndAddTime { get; set; }
    }
}
