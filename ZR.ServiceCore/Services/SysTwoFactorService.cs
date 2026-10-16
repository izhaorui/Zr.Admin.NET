using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Helper;
using Infrastructure.Model;
using ZR.Common;
using ZR.Infrastructure.Helper;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 双因子认证服务（TOTP 认证器 App）。
    ///
    /// 安全要点：
    /// 1) TOTP 密钥 AES-GCM 加密后落库，任何接口都不回传；
    /// 2) 同一时间步的验证码只允许成功一次（重放防护）；
    /// 3) 连续失败达到阈值按用户锁定，避免 6 位码被暴力枚举；
    /// 4) 恢复码只存 SHA256 哈希、一次性消费；
    /// 5) "记住此设备"独立记录，用户可逐台吊销，管理员重置时全部作废。
    /// </summary>
    [AppService(ServiceType = typeof(ISysTwoFactorService), ServiceLifetime = LifeTime.Transient)]
    public class SysTwoFactorService : BaseService<SysUser2faDevice>, ISysTwoFactorService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>恢复码数量</summary>
        private const int RecoveryCodeCount = 10;

        /// <summary>验证码重放标记缓存时长（分钟），需覆盖最大时间窗</summary>
        private const int StepMarkMinutes = 5;

        /// <summary>失败计数缓存时长（分钟）</summary>
        private const int FailCounterMinutes = 30;

        private readonly ISysUserService UserService;
        private readonly ISysUserMsgService UserMsgService;

        public SysTwoFactorService(ISysUserService userService, ISysUserMsgService userMsgService)
        {
            UserService = userService;
            UserMsgService = userMsgService;
        }

        private static TwoFactorSettings Settings => AppSettings.Bind("TwoFactor", new TwoFactorSettings());

        /// <summary>
        /// 是否已启用双因子。**只以库中标记为准**（代表用户/管理员的明确意图），
        /// 刻意不在此探测密钥能否解密：
        /// 若把"解密失败"当作"未启用"，一旦加密口令变更或密文损坏，登录就会静默跳过第二因子
        /// （fail-open，等于保护被无声摘掉）。密钥不可用的账号应在第二步校验时报错并拒绝登录
        /// （见 <see cref="VerifyCodeOrThrow"/>），即 fail-closed。
        /// </summary>
        public bool IsEnabled(SysUser user)
        {
            return user != null && user.TwoFactorEnabled == 1;
        }

        public bool IsEnabled(long userId)
        {
            return IsEnabled(GetUser(userId));
        }

        public bool RequiresTwoFactor(SysUser user, string clientId)
        {
            return IsEnabled(user) && !IsTrustedDevice(user.UserId, clientId);
        }

        public SysUser GetUserForAuth(long userId)
        {
            return GetUser(userId);
        }

        public TwoFactorInitDto Init(long userId, string userName)
        {
            var user = GetUser(userId);
            if (user == null) throw new CustomException("用户不存在");

            // 已启用时必须拒绝：Init 会把 TwoFactorEnabled 置 0 并清空恢复码与密钥。
            // 若不拦，任何持有有效会话的人（令牌泄露 / 会话劫持 / XSS）无需密码与动态码，
            // 就能持久地摘掉该账号的第二因子，把后续登录降级为"仅密码"。
            // 更换认证器请走 Disable（需密码 + 动态码/恢复码），或由管理员 Reset（有权限校验与审计）。
            if (IsEnabled(user))
            {
                throw new CustomException("当前账号已启用双因子认证，请先解绑后再重新绑定");
            }

            var secret = TotpHelper.GenerateSecret();

            // 先以"未启用"落库：校验通过才在 Enable 里置为启用，
            // 避免用户扫了码就退出，留下一个"启用中但没有恢复码"的半绑定状态。
            UserService.SaveTwoFactor(userId, 0, TotpHelper.ProtectSecret(secret), null, string.Empty);

            var settings = Settings;
            var account = string.IsNullOrEmpty(user.UserName) ? userName : user.UserName;
            return new TwoFactorInitDto
            {
                Secret = secret,
                Issuer = settings.Issuer,
                OtpauthUri = TotpHelper.BuildOtpAuthUri(settings.Issuer, account, secret)
            };
        }

        public TwoFactorEnableResultDto Enable(long userId, string password, string code)
        {
            var user = GetUser(userId);
            if (user == null) throw new CustomException("用户不存在");

            VerifyPassword(user, password);

            var secret = TotpHelper.UnprotectSecret(user.TwoFactorSecret);
            if (string.IsNullOrEmpty(secret))
            {
                throw new CustomException("请先获取双因子密钥");
            }

            if (TotpHelper.ValidateCode(secret, code) == null)
            {
                throw new CustomException("验证码错误，请确认认证器时间是否准确");
            }

            var plainCodes = Enumerable.Range(0, RecoveryCodeCount)
                .Select(_ => TotpHelper.GenerateRecoveryCode())
                .ToList();
            var hashes = plainCodes.Select(TotpHelper.HashRecoveryCode).ToList();

            UserService.SaveTwoFactor(userId, 1, user.TwoFactorSecret, DateTime.Now, JsonConvert.SerializeObject(hashes));
            CacheService.ClearTwoFactorFail(userId);

            logger.Warn($"双因子认证已启用：UserId={userId}, UserName={user.UserName}");
            return new TwoFactorEnableResultDto { RecoveryCodes = plainCodes };
        }

        public void Disable(long userId, string password, string code)
        {
            var user = GetUser(userId);
            if (user == null) throw new CustomException("用户不存在");
            if (!IsEnabled(user)) throw new CustomException("当前账号未启用双因子认证");

            VerifyPassword(user, password);
            VerifyCodeOrThrow(user, code);

            UserService.ClearTwoFactor(userId);
            ClearTrustedDevices(userId);

            logger.Warn($"双因子认证已解绑：UserId={userId}, UserName={user.UserName}");
        }

        public void Reset(long userId)
        {
            var user = GetUser(userId);
            if (user == null) throw new CustomException("用户不存在");

            UserService.ClearTwoFactor(userId);
            ClearTrustedDevices(userId);
            CacheService.ClearTwoFactorFail(userId);

            logger.Warn($"双因子认证被管理员重置：UserId={userId}, UserName={user.UserName}");
        }

        public void VerifyCodeOrThrow(SysUser user, string code)
        {
            if (user == null) throw new CustomException("用户不存在");
            if (string.IsNullOrWhiteSpace(code)) throw new CustomException("请输入验证码");

            if (CacheService.IsTwoFactorLocked(user.UserId))
            {
                throw new CustomException("验证失败次数过多，请稍后再试");
            }

            var secret = TotpHelper.UnprotectSecret(user.TwoFactorSecret);
            if (string.IsNullOrEmpty(secret))
            {
                throw new CustomException("双因子密钥不可用，请联系管理员重置");
            }

            var input = code.Trim();

            // 1) 认证器 6 位动态码
            if (input.Length == TotpHelper.DefaultDigits && input.All(char.IsAsciiDigit))
            {
                var step = TotpHelper.ValidateCode(secret, input);
                if (step != null)
                {
                    // 同一时间步只允许成功一次：否则 30 秒窗口内同一串码可被反复使用
                    if (CacheService.TryMarkTwoFactorStep(user.UserId, step.Value, StepMarkMinutes))
                    {
                        CacheService.ClearTwoFactorFail(user.UserId);
                        return;
                    }

                    OnFail(user, "验证码已被使用（疑似重放）");
                    throw new CustomException("该验证码已被使用，请等待认证器刷新后重试");
                }
            }
            // 2) 一次性恢复码（认证器丢失时的兜底通道）
            else if (ConsumeRecoveryCode(user, input))
            {
                CacheService.ClearTwoFactorFail(user.UserId);
                logger.Warn($"双因子恢复码已使用：UserId={user.UserId}, UserName={user.UserName}, 剩余={ParseRecoveryCodes(user.TwoFactorRecoveryCodes).Count}");
                return;
            }

            OnFail(user, "验证码错误");
            throw new CustomException("验证码错误");
        }

        public TwoFactorStatusDto GetStatus(long userId)
        {
            var user = GetUser(userId);
            if (user == null) throw new CustomException("用户不存在");

            var enabled = IsEnabled(user);
            return new TwoFactorStatusDto
            {
                Enabled = enabled,
                // 未启用时密钥字段为空属正常态，视为"无异常"：
                // 否则消费方会把"没绑过 2FA"误读成"密钥损坏"。
                SecretUsable = !enabled || IsSecretUsable(user),
                BoundAt = user.TwoFactorBoundAt,
                RecoveryCodeRemain = ParseRecoveryCodes(user.TwoFactorRecoveryCodes).Count,
                TrustedDeviceCount = CountTrustedDevices(userId)
            };
        }

        public List<TwoFactorDeviceDto> GetDevices(long userId)
        {
            var now = DateTime.Now;
            return Context.Queryable<SysUser2faDevice>()
                .Where(it => it.UserId == userId && it.TrustUntil > now)
                .OrderBy(it => it.Id, OrderByType.Desc)
                .Select(it => new TwoFactorDeviceDto
                {
                    Id = it.Id,
                    DeviceId = it.DeviceId,
                    DeviceName = it.DeviceName,
                    LoginIp = it.LoginIp,
                    TrustUntil = it.TrustUntil,
                    LastUsedAt = it.LastUsedAt,
                    CreatedAt = it.CreatedAt
                })
                .ToList();
        }

        public bool RemoveDevice(long userId, long deviceId)
        {
            // 条件里带 UserId：防止用他人设备 Id 越权吊销
            return Context.Deleteable<SysUser2faDevice>()
                .Where(it => it.Id == deviceId && it.UserId == userId)
                .ExecuteCommand() > 0;
        }

        public void ClearTrustedDevices(long userId)
        {
            Context.Deleteable<SysUser2faDevice>().Where(it => it.UserId == userId).ExecuteCommand();
        }

        public void TrustDevice(long userId, string clientId, string deviceName, string loginIp)
        {
            if (string.IsNullOrEmpty(clientId)) return;

            var days = Settings.TrustDeviceDays > 0 ? Settings.TrustDeviceDays : 7;
            var trustUntil = DateTime.Now.AddDays(days);

            var exist = Context.Queryable<SysUser2faDevice>()
                .Where(it => it.UserId == userId && it.DeviceId == clientId)
                .First();

            if (exist != null)
            {
                Context.Updateable<SysUser2faDevice>()
                    .SetColumns(it => it.TrustUntil == trustUntil)
                    .SetColumns(it => it.LastUsedAt == DateTime.Now)
                    .SetColumns(it => it.DeviceName == deviceName)
                    .SetColumns(it => it.LoginIp == loginIp)
                    .Where(it => it.Id == exist.Id)
                    .ExecuteCommand();
                return;
            }

            Insert(new SysUser2faDevice
            {
                UserId = userId,
                DeviceId = Truncate(clientId, 64),
                DeviceName = Truncate(deviceName, 200),
                LoginIp = Truncate(loginIp, 64),
                TrustUntil = trustUntil,
                LastUsedAt = DateTime.Now,
                CreatedAt = DateTime.Now
            });
        }

        #region 内部方法

        /// <summary>
        /// 密钥是否可用（可解密）。与"是否已启用"分开判定：
        /// 已启用但密钥不可用属于配置异常，必须 fail-closed（拒绝登录 + 提示重置），
        /// 同时让状态接口能把"已启用但密钥异常"如实告诉用户/管理员，而不是伪装成"未启用"。
        /// </summary>
        private static bool IsSecretUsable(SysUser user)
        {
            return !string.IsNullOrEmpty(user?.TwoFactorSecret)
                && TotpHelper.UnprotectSecret(user.TwoFactorSecret) != null;
        }

        private bool IsTrustedDevice(long userId, string clientId)
        {
            if (string.IsNullOrEmpty(clientId)) return false;

            var now = DateTime.Now;
            var device = Context.Queryable<SysUser2faDevice>()
                .Where(it => it.UserId == userId && it.DeviceId == clientId && it.TrustUntil > now)
                .First();
            if (device == null) return false;

            // 命中即刷新最后使用时间，便于用户识别长期未使用的僵尸设备
            Context.Updateable<SysUser2faDevice>()
                .SetColumns(it => it.LastUsedAt == now)
                .Where(it => it.Id == device.Id)
                .ExecuteCommand();
            return true;
        }

        private int CountTrustedDevices(long userId)
        {
            var now = DateTime.Now;
            return Queryable()
                .Where(it => it.UserId == userId && it.TrustUntil > now)
                .Count();
        }

        private bool ConsumeRecoveryCode(SysUser user, string code)
        {
            var hashes = ParseRecoveryCodes(user.TwoFactorRecoveryCodes);
            if (hashes.Count == 0) return false;
            if (!hashes.Remove(TotpHelper.HashRecoveryCode(code))) return false;

            UserService.SaveTwoFactor(user.UserId, 1, user.TwoFactorSecret, user.TwoFactorBoundAt,
                JsonConvert.SerializeObject(hashes));
            return true;
        }

        private static List<string> ParseRecoveryCodes(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();

            try
            {
                return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
            }
            catch (Exception)
            {
                // 恢复码列被外部写坏时按"无恢复码"处理，不影响 TOTP 正常校验
                return new List<string>();
            }
        }

        private static void VerifyPassword(SysUser user, string password)
        {
            if (string.IsNullOrEmpty(password)) throw new CustomException("密码不能为空");

            // 个人中心传明文、登录链路传 MD5，两种都接受（与 SysLoginService.Login 的处理一致）
            var md5 = password.Length == 32 ? password : NETCore.Encrypt.EncryptProvider.Md5(password);
            if (!string.Equals(user.Password, md5, StringComparison.OrdinalIgnoreCase))
            {
                throw new CustomException("密码错误");
            }
        }

        /// <summary>
        /// 记一次失败，达到阈值即锁定并发送告警。
        /// 锁定而不是直接拒绝，是为了让暴力枚举在有限次数内终止。
        /// </summary>
        private void OnFail(SysUser user, string reason)
        {
            var settings = Settings;
            var count = CacheService.IncreaseTwoFactorFail(user.UserId, FailCounterMinutes);
            var maxFail = settings.MaxFailCount > 0 ? settings.MaxFailCount : 5;
            if (count >= maxFail)
            {
                var lockMinutes = settings.LockMinutes > 0 ? settings.LockMinutes : 15;
                CacheService.SetTwoFactorLock(user.UserId, lockMinutes);
                logger.Error($"双因子校验失败次数超限，账号已锁定：UserId={user.UserId}, 失败次数={count}");
                AlertRepeatedFailure(user, count, lockMinutes);
                return;
            }

            logger.Warn($"双因子校验失败：UserId={user.UserId}, 累计失败={count}, 原因={reason}");
        }

        /// <summary>
        /// 2FA 连续失败告警：站内信（请求线程内同步落库）+ 邮件（后台线程，不阻塞登录响应）。
        ///
        /// 触发点只有"进入锁定"这一次，并叠加账号级限频（见 <see cref="TwoFactorSettings.AlertIntervalMinutes"/>），
        /// 避免攻击者靠反复触发锁定把用户邮箱刷爆。
        ///
        /// 站内信刻意复用 UserMsgType.SYSTEM：与异地登录、账号锁定提醒保持一致 ——
        /// 前端顶栏消息列表与未读红点都按 SYSTEM 过滤，换新类型会出现"有红点却看不到消息"。
        /// 正文只给"是否本人操作 + 该做什么"的引导，不放动态码/恢复码/令牌等凭据。
        /// </summary>
        private void AlertRepeatedFailure(SysUser user, long failCount, int lockMinutes)
        {
            var settings = Settings;
            var interval = settings.AlertIntervalMinutes > 0 ? settings.AlertIntervalMinutes : 60;
            if (!CacheService.TryMarkTwoFactorFailAlert(user.UserId, interval))
            {
                return; // 间隔内已告警过，本次不再重复打扰
            }

            var ip = App.HttpContext == null ? "未知" : HttpContextExtension.GetClientUserIp(App.HttpContext);
            var content = "⚠️ 双因子认证连续失败提醒\n\n"
                + "有人已输入正确的账号密码，但未通过动态码校验：\n\n"
                + $"账号：{user.UserName}\n"
                + $"失败次数：{failCount}（已临时锁定 {lockMinutes} 分钟）\n"
                + $"登录 IP：{ip}\n"
                + $"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n"
                + "这说明您的密码可能已泄露，请立即修改密码；如非本人操作，建议同时检查双因子绑定与可信设备。";

            if (settings.AlertBySiteMessage)
            {
                try
                {
                    UserMsgService.AddSysUserMsg(user.UserId, content, UserMsgType.SYSTEM);
                }
                catch (Exception ex)
                {
                    // 告警失败不能影响登录流程本身
                    logger.Error(ex, $"双因子失败告警（站内信）发送失败：UserId={user.UserId}");
                }
            }

            if (settings.AlertByEmail && !string.IsNullOrWhiteSpace(user.Email))
            {
                // 邮件走后台线程：SMTP 可能耗时数秒，挂在这里会拖慢登录失败响应
                var toEmail = user.Email;
                var fromName = settings.AlertMailFromName;
                _ = Task.Run(() => SendAlertMail(toEmail, fromName, "【安全提醒】双因子认证连续失败", content));
            }

            logger.Warn($"已发送双因子连续失败告警：UserId={user.UserId}, 失败次数={failCount}");
        }

        /// <summary>
        /// 发送告警邮件（后台线程执行）。
        /// MailOptions 未配置发件邮箱（FromEmail / Password 为空）时静默跳过、降级为仅站内信 ——
        /// 与工作流通知同一口径，未配置不算错误。
        /// </summary>
        private static void SendAlertMail(string toEmail, string fromName, string subject, string content)
        {
            try
            {
                var mailList = App.OptionsSetting?.MailOptions;
                if (mailList == null || mailList.Count == 0) return;

                var mailOptions = mailList.FirstOrDefault(m => m.FromName == fromName) ?? mailList[0];
                if (string.IsNullOrWhiteSpace(mailOptions.FromEmail) || string.IsNullOrWhiteSpace(mailOptions.Password))
                {
                    return;
                }

                var result = new MailHelper(mailOptions).SendMail(toEmail, subject, content);
                if (result == "fail")
                {
                    logger.Warn($"双因子失败告警邮件发送失败 To={MaskUtil.MaskEmail(toEmail)}");
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "双因子失败告警邮件发送异常");
            }
        }

        private SysUser GetUser(long userId)
        {
            return Context.Queryable<SysUser>().ClearFilter().Where(it => it.UserId == userId).First();
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value[..maxLength];
        }

        #endregion
    }
}
