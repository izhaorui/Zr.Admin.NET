using ZR.Model.System;

namespace ZR.Model
{
    /// <summary>
    /// 用户系统消息（多租户：统一存主库，按 TenantId 隔离）
    /// </summary>
    [SugarTable("sys_user_msg")]
    public class SysUserMsg : IMainDbEntity
    {
        /// <summary>
        /// 消息ID 
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = false)]
        public long MsgId { get; set; }

        /// <summary>
        /// 用户ID 
        /// </summary>
        public long? UserId { get; set; }

        /// <summary>
        /// 消息内容
        /// </summary>
        // 显式声明 nvarchar(2000)：内容含 ⚠️ 等 emoji，SQL Server 下 varchar 会把它们毁成问号，
        // 只有 nvarchar 能完整承载；2000 字对系统通知（告警/工作流通知等）余量充足，避免 max 的存储与索引代价。
        // MySQL/PgSQL/SQLite 原生支持 nvarchar(n)，Oracle 无 nvarchar，由 SqlsugarSetup 实体 AOP 适配为 nvarchar2(n)
        [SugarColumn(ColumnDescription = "消息内容", ColumnDataType = "nvarchar(2000)")]
        public string Content { get; set; }

        /// <summary>
        /// 是否已读 
        /// </summary>
        public int IsRead { get; set; }

        /// <summary>
        /// 添加时间 
        /// </summary>
        [SugarColumn(InsertServerTime = true)]
        public DateTime? AddTime { get; set; }

        /// <summary>
        /// 目标ID 
        /// </summary>
        public long? TargetId { get; set; }

        /// <summary>
        /// 消息类型 
        /// </summary>
        public UserMsgType MsgType { get; set; }

        /// <summary>
        /// 是否删除 
        /// </summary>
        public int IsDelete { get; set; }
        /// <summary>
        /// 来源用户
        /// </summary>
        public long FromUserid { get; set; }
        /// <summary>
        /// 关联用户
        /// </summary>
        [Navigate(NavigateType.OneToOne, nameof(FromUserid), nameof(SysUser.UserId))]//变量名不要等类名 
        public SysUser User { get; set; }

        /// <summary>
        /// 租户ID（多租户隔离标识）
        /// </summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string TenantId { get; set; }
    }
}