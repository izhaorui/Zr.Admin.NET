using System;

namespace ZR.Model.System.Dto
{
    /// <summary>
    /// 个人待办新增/编辑对象
    /// </summary>
    public class SysTodoDto
    {
        /// <summary>
        /// 待办ID（编辑时必传）
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 待办标题
        /// </summary>
        [Required(ErrorMessage = "待办标题不能为空")]
        public string Title { get; set; }

        /// <summary>
        /// 待办内容
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 优先级（1低 2中 3高）
        /// </summary>
        public int Priority { get; set; } = 2;

        /// <summary>
        /// 截止时间
        /// </summary>
        public DateTime? DueTime { get; set; }

        /// <summary>
        /// 提醒时间
        /// </summary>
        public DateTime? ReminderTime { get; set; }
    }

    /// <summary>
    /// 个人待办查询对象
    /// </summary>
    public class SysTodoQueryDto : PagerInfo
    {
        /// <summary>
        /// 关键词（标题/内容模糊匹配）
        /// </summary>
        public string KeyWord { get; set; }

        /// <summary>
        /// 状态（0未完成 1已完成，空=全部）
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// 优先级（1低 2中 3高）
        /// </summary>
        public int? Priority { get; set; }

        /// <summary>
        /// 截止时间起
        /// </summary>
        public DateTime? BeginTime { get; set; }

        /// <summary>
        /// 截止时间止
        /// </summary>
        public DateTime? EndTime { get; set; }
    }

    /// <summary>
    /// 个人待办状态切换对象
    /// </summary>
    public class SysTodoStatusDto
    {
        /// <summary>
        /// 待办ID
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 目标状态（0未完成 1已完成）
        /// </summary>
        public string Status { get; set; }
    }

    /// <summary>
    /// 个人待办统计结果
    /// </summary>
    public class SysTodoStatsDto
    {
        /// <summary>
        /// 总待办数
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// 未完成数
        /// </summary>
        public int Undone { get; set; }

        /// <summary>
        /// 今日到期数（未完成且截止日期为今天）
        /// </summary>
        public int DueToday { get; set; }

        /// <summary>
        /// 已逾期数（未完成且截止时间早于今天）
        /// </summary>
        public int Overdue { get; set; }
    }
}
