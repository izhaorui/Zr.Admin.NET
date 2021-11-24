using System;
using System.Collections.Generic;
using ZR.Model.Dto;
using ZR.Model.Models;

namespace ZR.Model.Dto
{
    /// <summary>
    /// 代码生成演示输入对象模型
    /// </summary>
    public class GendemoDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public int ShowStatus { get; set; }
        public int? Sex { get; set; }
        public int? Sort { get; set; }
        public DateTime? BeginTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Remark { get; set; }

    }

    /// <summary>
    /// 代码生成演示查询对象模型
    /// </summary>
    public class GendemoQueryDto: PagerInfo 
    {

        public DateTime? BeginTime { get; set; }
        public DateTime? EndTime { get; set; }
    }
}
