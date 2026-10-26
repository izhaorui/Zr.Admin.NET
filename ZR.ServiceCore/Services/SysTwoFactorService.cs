using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Helper;
using Infrastructure.Model;
using System.Security.Cryptography;
using System.Text;
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
    /// 5) "记住此设备"独立记录：信任只认服务端随机签发的高熵设备凭据（库中仅存 SHA256 哈希），
    ///    客户端指纹仅作展示/审计，用户可逐台吊销，管理员重置时全部作废。
    /// </summary>
    [AppService(ServiceType = typeof(ISysTwoFactorService), ServiceLifetime = LifeTime.Transient)]
    public class SysTwoFactorService : BaseService<SysUser2faDevice>, ISysTwoFactorService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// "未信任"的哨兵值。
        ///
        /// 刻意不把设备表的 TrustUntil 改成可空列：存量库若已建过该表，CodeFirst 补列逻辑不会帮我们
        /// ALTER 列的可空性，插入 NULL 会直接失败；而且 SetColumns(x == null) 生成的 SQL 在不同
        /// 数据库上表现也不一致。用一个明确落在 datetime 合法区间内、且恒早于当前时间的值最省事：
        /// 判定统一为 TrustUntil &gt; now，无需区分 NULL 分支。
        /// </summary>
        private static readonly DateTime NoTrustUntil = new DateTime(1900, 1, 1);

        /// <summary>恢复码数量</summary>
        private const int RecoveryCodeCount = 10;

        /// <summary>验证码重放标记缓存时长（分钟），需覆盖最大时间窗</summary>
        private const int StepMarkMinutes = 5;

        /// <summary>失败计数缓存时长（分钟）</summary>
        private const int FailCounterMinutes = 30;

        private readonly ISysUserService UserService;
        private readonly ISysUserMsgService UserMsgService;
        private readonly ISysWebAuthnService WebAuthnService;
        private readonly ZR.ServiceCore.Email.IEmailSendService EmailSendService;

        public SysTwoFactorService(ISysUserService userService, ISysUserMsgService userMsgService,
            ISysWebAuthnService webAuthnService, ZR.ServiceCore.Email.IEmailSendService emailSendService)
        {
            UserService = userService;
            UserMsgService = userMsgService;
            WebAuthnService = webAuthnService;
            EmailSendService = emailSendService;
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

        public bool RequiresTwoFactor(SysUser user, string deviceToken)
        {
            // 信任判定只认服务端签发的设备凭据：指纹等客户端标识是"名字"不是"钥匙"，当钥匙用等于摘掉 2FA
            return IsEnabled(user) && !IsTrustedDevice(user.UserId, HashDeviceToken(deviceToken));
        }

        /// <summary>
        /// 设备凭据哈希（SHA256 十六进制）。库中与双因子票据里只存哈希，明文仅在签发时返回一次。
        /// </summary>
        public string HashDeviceToken(string deviceToken)
        {
            if (string.IsNullOrWhiteSpace(deviceToken)) return string.Empty;
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(deviceToken)));
        }

        public SysUser GetUserForAuth(long userId)
        {
            return GetUser(userId);
        }

        /// <summary>
        /// 可用方法列表。
        ///
        /// TOTP 刻意**不按"密钥能否解密"过滤**：动态码与一次性恢复码共用同一个输入框，
        /// 密钥损坏的账号仍必须能靠恢复码进来（fail-closed 由 VerifyCodeOrThrow 负责，
        /// 不该在入口就把用户堵在一个没有输入框的弹框里）。
        ///
        /// WebAuthn 相反：只有该账号**确实绑定了凭据**才加入列表，避免前端渲染一个点了必然失败的入口。
        /// </summary>
        public List<string> GetAvailableMethods(SysUser user)
        {
            if (!IsEnabled(user)) return new List<string>();

            var methods = new List<string> { TwoFactorMethod.Totp };

            try
            {
                if (WebAuthnService.IsAvailable && WebAuthnService.HasCredential(user.UserId))
                {
                    methods.Add(TwoFactorMethod.WebAuthn);
                }
            }
            catch (Exception ex)
            {
                // 方法列表只影响"入口展示"：查凭据失败时降级为仅 TOTP，
                // 绝不能因此让已启用 2FA 的用户无法登录
                logger.Error(ex, $"查询 Passkey 凭据失败，本次仅提供 TOTP：UserId={user.UserId}");
            }

            return methods;
        }

        public string GetPreferredMethod(SysUser user)
        {
            var preferred = user?.TwoFactorPreferredMethod;
            if (string.IsNullOrEmpty(preferred)) return TwoFactorMethod.Default;

            // 值已不可用（例如手动改过库、或将来 WebAuthn 凭据被删光）时回退，
            // 避免前端按一个不存在的方法去渲染入口、用户无路可走。
            return GetAvailableMethods(user).Contains(preferred) ? preferred : TwoFactorMethod.Default;
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

            var step = TotpHelper.ValidateCode(secret, code);
            if (step == null)
            {
                throw new CustomException("验证码错误，请确认认证器时间是否准确");
            }
            // 绑定成功同样消费该时间步：否则同一串码在 30 秒窗口内还能再用于登录（与登录校验同口径防重放）
            if (!CacheService.TryMarkTwoFactorStep(userId, step.Value, StepMarkMinutes))
            {
                throw new CustomException("该验证码已被使用，请等待认证器刷新后重试");
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
            ClearDeviceTrust(userId);
            // 凭据在独立表里，只清 sys_user 字段会留下"仍能用 Passkey 登录"的静默旁路
            WebAuthnService.RemoveAllCredentials(userId);

            logger.Warn($"双因子认证已解绑：UserId={userId}, UserName={user.UserName}");
        }

        public void Reset(long userId)
        {
            var user = GetUser(userId);
            if (user == null) throw new CustomException("用户不存在");

            UserService.ClearTwoFactor(userId);
            ClearDeviceTrust(userId);
            // 管理员重置必须连凭据一起删：否则用户仍能凭 Passkey 登录，
            // "已重置"只是表面状态，属于最危险的那种静默失效
            WebAuthnService.RemoveAllCredentials(userId);
            CacheService.ClearTwoFactorFail(userId);

            logger.Warn($"双因子认证被管理员重置：UserId={userId}, UserName={user.UserName}");
        }

        /// <summary>
        /// 密码变更后的收敛动作：清空该账号全部设备的信任期，启用 2FA 时再站内信告知。
        ///
        /// 为什么必须清信任：改密码往往正是因为怀疑密码泄露，而"记住此设备"的信任期（默认 7 天）
        /// 会让已记住的设备在信任期内继续免动态码 —— 不清掉等于只改了密码、没切断会话路径。
        /// 设备登记记录保留（见 ClearDeviceTrust），因此重置后不会误报"新设备"。
        ///
        /// 刻意不在此处注销现有会话：单设备登录默认关闭（appsettings 的 SingleLogin=false），
        /// 且清空会话缓存会让 JwtAuthMiddleware 的会话校验 fail-open；
        /// 真正实现"改密即全端下线"需要独立于 SingleLogin 的凭据版本机制，不在本次范围。
        /// </summary>
        public void HandlePasswordChanged(long userId)
        {
            var user = GetUser(userId);
            if (user == null) return;

            ClearDeviceTrust(userId);

            // 未启用 2FA 时不存在"信任"概念，不必打扰用户
            if (!IsEnabled(user)) return;

            try
            {
                UserMsgService.AddSysUserMsg(userId,
                    "您的登录密码已变更。为确保安全，此前记住的设备已全部失效，下次登录需重新输入动态码。"
                    + "如非本人操作，请立即联系管理员。",
                    UserMsgType.SYSTEM);
            }
            catch (Exception ex)
            {
                // 通知失败不能影响改密本身
                logger.Error(ex, $"密码变更通知发送失败：UserId={userId}");
            }

            logger.Warn($"登录密码已变更，已清空该账号设备信任：UserId={userId}, UserName={user.UserName}");
        }

        public TwoFactorCredential VerifyCodeOrThrow(SysUser user, string code)
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
                // 密钥损坏（加密口令变更 / 密文损坏）时若已绑定 Passkey，账号并非无路可走：
                // 引导切换到 Passkey，而不是把用户彻底堵死在这一步
                if (HasPasskey(user))
                {
                    throw new CustomException("认证器密钥不可用，请改用 Passkey 验证");
                }

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
                        return TwoFactorCredential.Totp;
                    }

                    OnFail(user, "验证码已被使用（疑似重放）");
                    throw new CustomException("该验证码已被使用，请等待认证器刷新后重试");
                }
            }
            // 2) 一次性恢复码（认证器丢失时的兜底通道）
            else if (ConsumeRecoveryCode(user, input))
            {
                CacheService.ClearTwoFactorFail(user.UserId);

                var remain = ParseRecoveryCodes(user.TwoFactorRecoveryCodes).Count;
                logger.Warn($"双因子恢复码已使用：UserId={user.UserId}, UserName={user.UserName}, 剩余={remain}");
                AlertRecoveryCodeUsed(user, remain);
                return TwoFactorCredential.RecoveryCode;
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
                TrustedDeviceCount = CountTrustedDevices(userId),
                Methods = GetAvailableMethods(user),
                PreferredMethod = GetPreferredMethod(user)
            };
        }

        /// <summary>
        /// 登录设备列表（**全部已登记设备**，不只是受信任的）。
        /// 用户判断"有没有异常登录"靠的就是"我登录过哪些设备"，只列受信任设备会漏掉痕迹。
        /// </summary>
        public List<TwoFactorDeviceDto> GetDevices(long userId)
        {
            var now = DateTime.Now;
            var list = Context.Queryable<SysUser2faDevice>()
                .Where(it => it.UserId == userId)
                .OrderBy(it => it.LastUsedAt, OrderByType.Desc)
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

            // 信任状态在内存里算：设备数受 MaxDeviceRecords 约束（默认 20），成本可忽略，
            // 也比在 SQL 里拼 TrustUntil 比较表达式更不易踩各数据库的方言差异。
            // 未信任在库里存的是哨兵值（见 NoTrustUntil），对外统一暴露 null，
            // 免得前端显示 1900-01-01 这种内部约定。
            foreach (var item in list)
            {
                if (item.TrustUntil.HasValue && item.TrustUntil.Value > now)
                {
                    item.Trusted = true;
                }
                else
                {
                    item.TrustUntil = null;
                }
            }

            return list;
        }

        public bool RemoveDevice(long userId, long deviceId)
        {
            // 条件里带 UserId：防止用他人设备 Id 越权移除
            return Context.Deleteable<SysUser2faDevice>()
                .Where(it => it.Id == deviceId && it.UserId == userId)
                .ExecuteCommand() > 0;
        }

        /// <summary>
        /// 清空全部设备的信任期（解绑 2FA / 管理员重置）。
        /// 刻意保留设备登记记录：重置后用户从常用设备登录不会被误报"新设备"，也仍能看到历史设备；
        /// 安全上只需确保"没有任何设备再被信任"。
        /// </summary>
        public void ClearDeviceTrust(long userId)
        {
            // 必须先赋值给局部变量再进表达式：SqlSugar 的表达式解析器拒绝翻译私有字段
            // （会抛 Field "NoTrustUntil" can't be private），而局部变量会被当成 SQL 参数。
            var noTrust = NoTrustUntil;

            Context.Updateable<SysUser2faDevice>()
                .SetColumns(it => it.TrustUntil == noTrust)
                .Where(it => it.UserId == userId)
                .ExecuteCommand();
        }

        public DeviceRecordResult RecordLoginDevice(long userId, string deviceTokenHash, string clientId, string deviceName, string loginIp, bool trust = false)
        {
            clientId = Truncate(clientId, 64);
            var now = DateTime.Now;
            var trustUntil = trust
                ? now.AddDays(Settings.TrustDeviceDays > 0 ? Settings.TrustDeviceDays : 7)
                : NoTrustUntil;

            // 凭据只能由服务端签发：客户端带来的哈希只用于"认出这台设备"，绝不采信、绝不写库。
            // 勾选"记住"时一律签发新凭据并替换旧值（轮换），旧凭据当场作废。
            var newToken = trust ? GenerateDeviceToken() : null;
            var newHash = trust ? HashDeviceToken(newToken) : null;

            SysUser2faDevice exist = null;
            if (!string.IsNullOrEmpty(deviceTokenHash))
            {
                exist = Context.Queryable<SysUser2faDevice>()
                    .Where(it => it.UserId == userId && it.DeviceTokenHash == deviceTokenHash)
                    .First();
            }

            // 未携带凭据（首次登录/清了本地存储）时按指纹归并登记记录，同一台设备不重复建记录、不重复提醒。
            // 但勾选"记住"时绝不按指纹归并：指纹是客户端可控的展示字段，按它归并会让伪造者顶替他人设备记录。
            if (exist == null && !trust && !string.IsNullOrWhiteSpace(clientId))
            {
                exist = Context.Queryable<SysUser2faDevice>()
                    .Where(it => it.UserId == userId && it.DeviceId == clientId)
                    .First();
            }

            if (exist != null)
            {
                var update = Context.Updateable<SysUser2faDevice>()
                    .SetColumns(it => it.LastUsedAt == now)
                    .SetColumns(it => it.DeviceName == Truncate(deviceName, 200))
                    .SetColumns(it => it.LoginIp == Truncate(loginIp, 64));

                // 只有本次要求"记住"时才改写信任期与凭据：普通登录不能把已有的信任意外清掉
                if (trust)
                {
                    update = update.SetColumns(it => it.DeviceTokenHash == newHash)
                        .SetColumns(it => it.TrustUntil == trustUntil);
                }

                update.Where(it => it.Id == exist.Id).ExecuteCommand();
                return new DeviceRecordResult { DeviceToken = newToken };
            }

            // 既无凭据可认、指纹也没有，且未要求"记住"：无法识别设备，不登记也不提醒
            if (!trust && string.IsNullOrWhiteSpace(clientId))
            {
                return new DeviceRecordResult();
            }

            Insert(new SysUser2faDevice
            {
                UserId = userId,
                DeviceId = clientId ?? string.Empty,
                DeviceTokenHash = newHash,
                DeviceName = Truncate(deviceName, 200),
                LoginIp = Truncate(loginIp, 64),
                TrustUntil = trustUntil,
                LastUsedAt = now,
                CreatedAt = now
            });

            TrimDeviceRecords(userId);
            AlertNewDevice(userId, deviceName, loginIp, trust);
            return new DeviceRecordResult { IsNewDevice = true, DeviceToken = newToken };
        }

        /// <summary>
        /// 设备记录上限：按最后使用时间淘汰最旧的非信任设备。
        /// 设备标识来自客户端、可被伪造，没有上限就有刷表风险；受信任设备始终保留。
        /// </summary>
        private void TrimDeviceRecords(long userId)
        {
            var max = Settings.MaxDeviceRecords > 0 ? Settings.MaxDeviceRecords : 20;
            var total = Context.Queryable<SysUser2faDevice>().Where(it => it.UserId == userId).Count();
            if (total <= max) return;

            var now = DateTime.Now;
            var staleIds = Context.Queryable<SysUser2faDevice>()
                .Where(it => it.UserId == userId && it.TrustUntil <= now)
                .OrderBy(it => it.LastUsedAt)
                .Take(total - max)
                .Select(it => it.Id)
                .ToList();

            if (staleIds.Count == 0) return;

            Context.Deleteable<SysUser2faDevice>().Where(it => staleIds.Contains(it.Id)).ExecuteCommand();
        }

        /// <summary>
        /// 新设备登录提醒（站内信 + 邮件）。
        ///
        /// 限频：无痕/清缓存会让设备指纹变化，正常用户也可能连续触发；
        /// 且与"连续失败告警"用**独立 cache key**，两类告警互不抑制。
        /// 正文只给"是否本人 + 该做什么"的引导，不放动态码/恢复码/密钥。
        /// </summary>
        private void AlertNewDevice(long userId, string deviceName, string loginIp, bool trusted)
        {
            var settings = Settings;
            if (!settings.AlertOnNewDevice) return;

            var interval = settings.AlertIntervalMinutes > 0 ? settings.AlertIntervalMinutes : 60;
            if (!CacheService.TryMarkNewDeviceAlert(userId, interval)) return;

            var user = GetUser(userId);
            if (user == null) return;

            var content = "⚠️ 新设备登录提醒\n\n"
                + "您的账号在一台此前未使用过的设备上登录成功：\n\n"
                + $"账号：{user.UserName}\n"
                + $"设备：{deviceName}\n"
                + $"登录 IP：{loginIp}\n"
                + $"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n"
                + (trusted ? "该设备已按您的勾选加入信任列表，信任期内登录免动态码。\n" : string.Empty)
                + "\n如非本人操作，请立即修改密码，并在「个人中心 → 安全设置」中移除该设备、检查双因子绑定。";

            if (settings.AlertBySiteMessage)
            {
                try
                {
                    UserMsgService.AddSysUserMsg(userId, content, UserMsgType.SYSTEM);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"新设备登录告警（站内信）发送失败：UserId={userId}");
                }
            }

            if (settings.AlertByEmail && !string.IsNullOrWhiteSpace(user.Email))
            {
                var toEmail = user.Email;
                var fromName = settings.AlertMailFromName;
                // 入队由 SendAlertMail 内部用独立连接异步执行，这里只做轻量参数准备
                SendAlertMail(toEmail, fromName, "【安全提醒】新设备登录", content);
            }

            logger.Warn($"已发送新设备登录告警：UserId={userId}, 信任={trusted}");
        }

        #region 内部方法

        /// <summary>
        /// 该账号是否存在可用的 Passkey 凭据。
        /// 查询失败按"没有"处理：它只用于决定提示文案与密钥损坏时的豁免，
        /// 不能让它反过来影响 TOTP 主链路的可用性。
        /// </summary>
        private bool HasPasskey(SysUser user)
        {
            try
            {
                return user != null && WebAuthnService.IsAvailable && WebAuthnService.HasCredential(user.UserId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"查询 Passkey 凭据失败：UserId={user?.UserId}");
                return false;
            }
        }

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

        /// <summary>
        /// 生成设备凭据明文：256 bit 随机 + base64url。不可猜测、不可枚举，泄露也无法反推其它凭据。
        /// </summary>
        private static string GenerateDeviceToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private bool IsTrustedDevice(long userId, string deviceTokenHash)
        {
            if (string.IsNullOrEmpty(deviceTokenHash)) return false;

            var now = DateTime.Now;
            // 只按凭据哈希匹配：指纹等客户端标识可伪造，不能作为"这台设备"的证明
            var device = Context.Queryable<SysUser2faDevice>()
                .Where(it => it.UserId == userId && it.DeviceTokenHash == deviceTokenHash && it.TrustUntil > now)
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
            var oldJson = user.TwoFactorRecoveryCodes;
            var hashes = ParseRecoveryCodes(oldJson);
            if (hashes.Count == 0) return false;
            if (!hashes.Remove(TotpHelper.HashRecoveryCode(code))) return false;

            // CAS 条件更新：只有"库中恢复码仍等于读取时的列表"的更新才生效。
            // 并发提交同一恢复码时只有一方更新成功 —— 读-改-写整列覆盖会让两个请求都判定成功，
            // 一次性恢复码就被用两次；更新行数必须显式检查。
            var newJson = JsonConvert.SerializeObject(hashes);
            var affected = Context.Updateable<SysUser>()
                .SetColumns(it => it.TwoFactorRecoveryCodes == newJson)
                .Where(it => it.UserId == user.UserId && it.TwoFactorRecoveryCodes == oldJson)
                .ExecuteCommand();
            return affected == 1;
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
            var md5 = password.Length == 32 ? password : SecurityHelper.Md5(password);
            if (!string.Equals(user.Password, md5, StringComparison.OrdinalIgnoreCase))
            {
                throw new CustomException("密码错误");
            }
        }

        /// <summary>
        /// 恢复码被使用时的告警（站内信 + 邮件，渠道同其它告警）。
        ///
        /// 恢复码只在"认证器不可用"时才该被用到：可能是用户换机（正常），
        /// 也可能是攻击者拿到了恢复码（高危）。无论哪种都值得让用户知道——
        /// 而且这是**唯一**能让用户发现"有人在用我的恢复码"的渠道。
        /// 不做限频：每个恢复码一次性、用完即销，天然不会重复触发。
        ///
        /// 注意本方法在校验成功分支内调用，因此**不能让它抛异常**影响登录，
        /// 各渠道都单独兜了异常。
        /// </summary>
        private void AlertRecoveryCodeUsed(SysUser user, int remain)
        {
            var settings = Settings;
            var content = "⚠️ 双因子恢复码使用提醒\n\n"
                + "您的账号刚刚使用了一个一次性恢复码完成身份验证：\n\n"
                + $"账号：{user.UserName}\n"
                + $"剩余恢复码：{remain} 个\n"
                + $"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n"
                + "如果您本人正在换机，请忽略本条，并尽快在「个人中心 → 安全设置」重新绑定认证器；"
                + "如果不是您本人操作，请立即修改密码并让管理员重置双因子认证。";

            if (settings.AlertBySiteMessage)
            {
                try
                {
                    UserMsgService.AddSysUserMsg(user.UserId, content, UserMsgType.SYSTEM);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"恢复码使用告警（站内信）发送失败：UserId={user.UserId}");
                }
            }

            if (settings.AlertByEmail && !string.IsNullOrWhiteSpace(user.Email))
            {
                var toEmail = user.Email;
                var fromName = settings.AlertMailFromName;
                SendAlertMail(toEmail, fromName, "【安全提醒】使用了双因子恢复码", content);
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
                // 入队由 SendAlertMail 内部异步执行（独立连接），SMTP 投递由后台队列完成
                var toEmail = user.Email;
                var fromName = settings.AlertMailFromName;
                SendAlertMail(toEmail, fromName, "【安全提醒】双因子认证连续失败", content);
            }

            logger.Warn($"已发送双因子连续失败告警：UserId={user.UserId}, 失败次数={failCount}");
        }

        /// <summary>
        /// 发送告警邮件。
        /// MailOptions 未配置发件邮箱（FromEmail / Password 为空）时静默跳过、降级为仅站内信 ——
        /// 与工作流通知同一口径，未配置不算错误。
        /// 落库/入队由 IEmailSendService 在独立连接的后台任务里完成（不复用请求连接），
        /// SMTP 投递由后台队列执行并在失败时退避重试。
        /// </summary>
        private void SendAlertMail(string toEmail, string fromName, string subject, string content)
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

                EmailSendService.EnqueueTemplateDetached(ZR.Common.Email.EmailTemplateDefaults.SecurityAlert,
                    new[] { toEmail },
                    new Dictionary<string, object>
                    {
                        ["subject"] = subject,
                        ["content"] = content,
                    },
                    "security",
                    mailOptions.FromName);
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
