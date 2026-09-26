using System.ComponentModel;

namespace ZR.Model.System.Enums
{
    /// <summary>
    /// 逻辑删除标志。
    /// 数据库列保持 int 类型，历史取值为 0=存在、2=删除（沿用若依约定），
    /// 此处用枚举替代魔法数字，避免误写为 1 等非法值导致语义不清。
    /// </summary>
    public enum DelFlagEnum
    {
        /// <summary>
        /// 存在（未删除）
        /// </summary>
        [Description("存在")]
        Normal = 0,

        /// <summary>
        /// 已删除
        /// </summary>
        [Description("已删除")]
        Deleted = 2
    }
}
