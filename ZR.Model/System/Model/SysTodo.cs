using System;

namespace ZR.Model.System
{
    /// <summary>
    /// 个人待办表（租户库表，不加 IMainDbEntity，由框架按当前租户自动路由 db context）
    /// </summary>
    [SugarTable("sys_todo", "个人待办表")]
    [SugarIndex("idx_todo_user", nameof(UserId), OrderByType.Asc)]
    public class SysTodo : SysBase
    {
        /// <summary>
        /// 待办ID
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// 归属用户ID（仅当前用户可见自己的待办）
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long UserId { get; set; }

        /// <summary>
        /// 待办标题
        /// </summary>
        [SugarColumn(Length = 200, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string Title { get; set; }

        /// <summary>
        /// 待办内容
        /// </summary>
        [SugarColumn(Length = 1000, IsNullable = true)]
        public string Content { get; set; }

        /// <summary>
        /// 状态（0未完成 1已完成）
        /// </summary>
        [SugarColumn(Length = 1, DefaultValue = "0")]
        public string Status { get; set; } = "0";

        /// <summary>
        /// 优先级（1低 2中 3高）
        /// </summary>
        [SugarColumn(DefaultValue = "2")]
        public int Priority { get; set; } = 2;

        /// <summary>
        /// 截止时间
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? DueTime { get; set; }

        /// <summary>
        /// 提醒时间
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? ReminderTime { get; set; }

        /// <summary>
        /// 完成时间
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? FinishTime { get; set; }

        /// <summary>
        /// 是否已提醒（0未提醒 1已提醒），用于待办到期提醒去重，避免刷新/重连重复推送
        /// </summary>
        [SugarColumn(IsNullable = true, DefaultValue = "0")]
        public int? Reminded { get; set; } = 0;
    }
}
