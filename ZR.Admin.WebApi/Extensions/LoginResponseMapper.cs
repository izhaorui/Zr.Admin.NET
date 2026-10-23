using ZR.Model.System.Dto;

namespace ZR.Admin.WebApi.Extensions
{
    /// <summary>
    /// 登录结果 → 响应体的唯一映射点。
    ///
    /// 账号密码登录、手机号登录、三方登录必须共用同一份映射，否则会出现
    /// "某个入口漏了 2FA 分支"这类契约漂移，前端就得为每个入口写一套判断。
    /// 放在 WebApi 而不是 Infrastructure：Infrastructure 不引用 ZR.Model，拿不到 LoginResultDto。
    /// </summary>
    internal static class LoginResponseMapper
    {
        public static ApiResult Map(LoginResultDto result)
        {
            switch (result.Status)
            {
                case LoginResultStatus.TwoFactorRequired:
                {
                    var apiResult = new ApiResult((int)ResultCode.MFA_REQUIRED, "需要双因子验证");
                    apiResult.Put("mfaRequired", true);
                    apiResult.Put("ticket", result.Ticket);
                    apiResult.Put("expireMinutes", result.ExpireMinutes);
                    // 可用方法与首选方法由服务端下发，前端只按列表渲染入口：
                    // WebAuthn 落地时后端加一项即可，前端与接口契约都不用再改。
                    apiResult.Put("methods", result.Methods);
                    apiResult.Put("preferredMethod", result.PreferredMethod);
                    return apiResult;
                }
                case LoginResultStatus.TicketExpired:
                    return new ApiResult((int)ResultCode.LOGIN_ERROR, "双因子验证已超时，请重新登录");

                case LoginResultStatus.AccountUnavailable:
                    return new ApiResult((int)ResultCode.LOGIN_ERROR, "账号不可用");

                case LoginResultStatus.BindRequired:
                {
                    // 刻意不是"登录失败"：用户凭第三方身份通过了验证，只是本地没有对应账号。
                    // 用独立标记让前端引导"先登录再绑定"，而不是原地重试把 code 试废。
                    var apiResult = new ApiResult((int)ResultCode.CUSTOM_ERROR,
                        "该第三方账号尚未绑定系统账号，请先登录后在账号绑定中完成绑定");
                    apiResult.Put("bindRequired", true);
                    return apiResult;
                }
                default:
                {
                    ApiResult apiResult = new((int)ResultCode.SUCCESS, "success", result.Token);
                    if (!string.IsNullOrEmpty(result.Notice))
                    {
                        apiResult.Put("notice", result.Notice);
                    }
                    // "记住此设备"签发的设备凭据只在此返回一次（服务端只存哈希），客户端保存后下次登录带回
                    if (!string.IsNullOrEmpty(result.DeviceToken))
                    {
                        apiResult.Put("deviceToken", result.DeviceToken);
                    }

                    return apiResult;
                }
            }
        }
    }
}