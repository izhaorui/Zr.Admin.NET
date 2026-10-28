namespace ZR.Model.Enum
{
    public enum LoginAuditStatusEnum
    {
        Success = 0,
        Failed = 1,
    }

    public static class LoginAuditStatusExtensions
    {
        public static string ToDbValue(this LoginAuditStatusEnum status)
        {
            return ((int)status).ToString();
        }
    }
}
