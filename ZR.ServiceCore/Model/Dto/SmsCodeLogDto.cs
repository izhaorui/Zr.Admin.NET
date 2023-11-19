using System.ComponentModel.DataAnnotations;
using MiniExcelLibs.Attributes;

namespace ZR.Model.Dto
{
    /// <summary>
    /// 短信验证码记录查询对象
    /// </summary>
    public class SmscodeLogQueryDto : PagerInfo 
    {
        public int? Userid { get; set; }
        public long? PhoneNum { get; set; }
        public DateTime? BeginAddTime { get; set; }
        public DateTime? EndAddTime { get; set; }
        public int? SendType { get; set; }
    }

    /// <summary>
    /// 短信验证码记录输入输出对象
    /// </summary>
    public class SmsCodeLogDto
    {
        [Required(ErrorMessage = "Id不能为空")]
        [ExcelColumn(Name = "Id")]
        [ExcelColumnName("Id")]
        [JsonConverter(typeof(ValueToStringConverter))]
        public long Id { get; set; }

        [ExcelColumn(Name = "短信验证码")]
        [ExcelColumnName("短信验证码")]
        public string SmsCode { get; set; }

        [ExcelColumn(Name = "用户id")]
        [ExcelColumnName("用户id")]
        public int? Userid { get; set; }

        [ExcelColumn(Name = "手机号")]
        [ExcelColumnName("手机号")]
        [JsonConverter(typeof(ValueToStringConverter))]
        public long? PhoneNum { get; set; }

        [ExcelColumn(Name = "短信内容")]
        [ExcelColumnName("短信内容")]
        public string SmsContent { get; set; }

        [ExcelColumn(Name = "添加时间", Format = "yyyy-MM-dd HH:mm:ss")]
        [ExcelColumnName("添加时间")]
        public DateTime? AddTime { get; set; }

        [ExcelColumn(Name = "用户IP")]
        [ExcelColumnName("用户IP")]
        public string UserIP { get; set; }

        [ExcelColumn(Name = "发送类型")]
        [ExcelColumnName("发送类型")]
        public int? SendType { get; set; }
        [ExcelColumn(Name = "地理位置")]
        [ExcelColumnName("地理位置")]
        public string Location { get; set; }
    }
}