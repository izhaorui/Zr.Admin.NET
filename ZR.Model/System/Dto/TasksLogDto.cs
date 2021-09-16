using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System.Dto
{
    public class TasksLogQueryDto
    {
        /// <summary>
        /// 描述 : 查询字符串 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        //[Display(Name = "查询字符串")]
        public string Name{ get; set; }
        public string JobName { get; set; }
        public string JobId { get; set; }
        public string JobGroup { get; set; }
        public string Status { get; set; }
        public DateTime? BeginTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    public class TasksLogDto
    {

    }
}
