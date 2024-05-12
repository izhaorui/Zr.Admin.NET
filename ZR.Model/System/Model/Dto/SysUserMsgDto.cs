using ZR.Model.Content.Dto;

namespace ZR.Model.Dto
{
    /// <summary>
    /// 用户系统消息查询对象
    /// </summary>
    public class SysUserMsgQueryDto : PagerInfo 
    {
        public long? UserId { get; set; }
        public int? IsRead { get; set; }
        public long? ClassifyId { get; set; }
        public UserMsgType? MsgType { get; set; }
    }

    /// <summary>
    /// 用户系统消息输入输出对象
    /// </summary>
    public class SysUserMsgDto
    {
        [Required(ErrorMessage = "消息ID不能为空")]
        [ExcelColumn(Name = "消息ID")]
        [ExcelColumnName("消息ID")]
        [JsonConverter(typeof(ValueToStringConverter))]
        public long MsgId { get; set; }

        [ExcelColumn(Name = "用户ID")]
        [ExcelColumnName("用户ID")]
        public long? UserId { get; set; }

        [ExcelColumn(Name = "消息内容")]
        [ExcelColumnName("消息内容")]
        public string Content { get; set; }

        [ExcelColumn(Name = "是否已读")]
        [ExcelColumnName("是否已读")]
        public int? IsRead { get; set; }

        [ExcelColumn(Name = "添加时间", Format = "yyyy-MM-dd HH:mm:ss", Width = 20)]
        [ExcelColumnName("添加时间")]
        public DateTime? AddTime { get; set; }

        [ExcelColumn(Name = "目标ID")]
        [ExcelColumnName("目标ID")]
        public long? TargetId { get; set; }

        [ExcelColumn(Name = "分类ID")]
        [ExcelColumnName("分类ID")]
        public long? ClassifyId { get; set; }

        [ExcelColumn(Name = "消息类型")]
        [ExcelColumnName("消息类型")]
        public string MsgType { get; set; }

        [ExcelColumn(Name = "是否删除")]
        [ExcelColumnName("是否删除")]
        public int? IsDelete { get; set; }

        public UserDto User { get; set; }
        [JsonIgnore]
        [ExcelColumn(Name = "是否已读")]
        public string IsReadLabel { get; set; }
        public string ImgUrl { get; set; }
    }
}