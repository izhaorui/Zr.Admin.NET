using System;

namespace ZR.Model.System.Dto
{
    public class TasksLogQueryDto
    {
        /// <summary>
        /// 查询字符串
        /// </summary>
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
