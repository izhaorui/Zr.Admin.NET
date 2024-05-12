namespace ZR.Model.Dto
{
    /// <summary>
    /// 用户在线时长查询对象
    /// </summary>
    public class UserOnlineLogQueryDto : PagerInfo 
    {
        public int? UserId { get; set; }
        public string UserIP { get; set; }
        public DateTime? BeginAddTime { get; set; }
        public DateTime? EndAddTime { get; set; }
    }

    /// <summary>
    /// 用户在线时长输入输出对象
    /// </summary>
    public class UserOnlineLogDto
    {
        [Required(ErrorMessage = "Id不能为空")]
        [ExcelColumn(Name = "Id")]
        [ExcelColumnName("Id")]
        [JsonConverter(typeof(ValueToStringConverter))]
        public long Id { get; set; }

        [Required(ErrorMessage = "用户id不能为空")]
        [ExcelColumn(Name = "用户id")]
        [ExcelColumnName("用户id")]
        public long UserId { get; set; }

        [Required(ErrorMessage = "在线时长(分)不能为空")]
        [ExcelColumn(Name = "在线时长(分)")]
        [ExcelColumnName("在线时长(分)")]
        public double OnlineTime { get; set; }

        [ExcelColumn(Name = "结束时间", Format = "yyyy-MM-dd HH:mm:ss", Width = 20)]
        [ExcelColumnName("结束时间")]
        public DateTime? AddTime { get; set; }

        [ExcelColumn(Name = "地址位置")]
        [ExcelColumnName("地址位置")]
        public string Location { get; set; }

        [ExcelColumn(Name = "用户IP")]
        [ExcelColumnName("用户IP")]
        public string UserIP { get; set; }

        [ExcelColumn(Name = "备注")]
        [ExcelColumnName("备注")]
        public string Remark { get; set; }

        [ExcelColumn(Name = "登录时间", Format = "yyyy-MM-dd HH:mm:ss", Width = 20)]
        [ExcelColumnName("登录时间")]
        public DateTime? LoginTime { get; set; }

        [ExcelColumn(Name = "今日在线时长")]
        [ExcelColumnName("今日在线时长")]
        public string TodayOnlineTime { get; set; }

        [ExcelColumn(Name = "登录平台")]
        [ExcelColumnName("登录平台")]
        public string Platform { get; set; }
        [ExcelColumn(Name = "用户昵称")]
        public string NickName { get; set; }
    }
}