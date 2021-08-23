using Infrastructure.Enums;

namespace Infrastructure.Attribute
{
    /// <summary>
    /// 自定义操作日志记录注解
    /// </summary>
    public class LogAttribute : System.Attribute
    {
        public string Title { get; set; }
        public BusinessType BusinessType { get; set; }

        public LogAttribute() { }

        public LogAttribute(string name)
        {
            Title = name;
        }
        public LogAttribute(string name, BusinessType businessType)
        {
            Title = name;
            BusinessType = businessType;
        }
    }
}
