using System.Text;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Model;
using Newtonsoft.Json.Linq;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// WebAuthn / Passkey 第二因子实现。
    ///
    /// 安全底线：
    /// 1) RP ID 与 Origins **只从配置读**，绝不从请求头推导 —— 否则伪造 Host 即可绕过防钓鱼校验；
    /// 2) challenge 服务端生成、一次性原子消费，并绑定"用户 + 用途 + 票据"，注册与认证不可混用；
    /// 3) 校验一律使用服务端缓存的原始 options，不采信前端回传的 options；
    /// 4) 签名计数器带旧值做 CAS 更新，克隆凭据的并发重放会被挡下；
    /// 5) 未配置 Origins 时功能整体关闭（fail-closed），不出现"开着但校验不了"的半开状态。
    /// </summary>
    [AppService(ServiceType = typeof(ISysWebAuthnService), ServiceLifetime = LifeTime.Transient)]
    public class SysWebAuthnService : BaseService<SysUserWebAuthnCredential>, ISysWebAuthnService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>凭据 ID 上限（与实体列宽一致）</summary>
        private const int MaxCredentialIdLength = 512;

        /// <summary>断言 JSON 大小上限：正常约 1~3 KB，超过这个量级必是异常构造</summary>
        private const int MaxAssertionJsonLength = 32 * 1024;

        /// <summary>设备名称上限（与实体列宽一致）</summary>
        private const int MaxDeviceNameLength = 200;

        private static TwoFactorSettings Settings => AppSettings.Bind("TwoFactor", new TwoFactorSettings());

        public bool IsAvailable
        {
            get
            {
                var settings = Settings;
                if (settings == null || !settings.WebAuthnEnabled) return false;

                // 与 BuildFido2 同一判定口径：Origin 必须能被 RP ID 覆盖。
                // 否则前端会渲染出一个点了必然失败的入口（浏览器对 RP ID 不匹配不给任何可读错误）。
                var rpId = GetRpId(settings);
                if (string.IsNullOrEmpty(rpId)) return false;

                return GetUsableOrigins(settings, rpId).Count > 0;
            }
        }

        public bool HasCredential(long userId)
        {
            if (!IsAvailable || userId <= 0) return false;

            return Context.Queryable<SysUserWebAuthnCredential>().Any(it => it.UserId == userId);
        }

        public TwoFactorWebAuthnOptionsDto MakeRegisterOptions(long userId, string userName, string displayName, string password)
        {
            if (!IsAvailable) throw new CustomException("当前系统未启用 Passkey，请联系管理员");

            var user = GetUser(userId);
            if (user == null) throw new CustomException("用户不存在");

            VerifyPassword(user, password);

            var settings = Settings;
            var max = MaxCredentials(settings);

            // 已绑定的凭据放进 ExcludeCredentials：同一认证器重复注册会被浏览器直接拦下，
            // 比事后发现"同一设备两条记录"更早地给出明确提示
            var existIds = Context.Queryable<SysUserWebAuthnCredential>()
                .Where(it => it.UserId == userId)
                .Select(it => it.CredentialId)
                .ToList();

            if (existIds.Count >= max)
            {
                throw new CustomException($"最多绑定 {max} 个 Passkey，请先删除不用的凭据");
            }

            var fidoUser = new Fido2User
            {
                // Id 是写入认证器的用户句柄：用用户主键的稳定字节表示，换用户名不会导致凭据错位
                Id = Encoding.UTF8.GetBytes(userId.ToString()),
                Name = user.UserName ?? userName,
                DisplayName = string.IsNullOrWhiteSpace(user.NickName)
                    ? (user.UserName ?? userName)
                    : user.NickName
            };

            var options = BuildFido2(settings).RequestNewCredential(new RequestNewCredentialParams
            {
                User = fidoUser,
                ExcludeCredentials = existIds
                    .Select(id => new PublicKeyCredentialDescriptor(Base64UrlDecode(id)))
                    .ToList(),
                AuthenticatorSelection = new AuthenticatorSelection
                {
                    // Preferred 而非 Required：企业里可能用安全密钥（U-Key），
                    // 强制 ResidentKey 会把这类认证器排除在外
                    ResidentKey = ResidentKeyRequirement.Preferred,
                    // Required：必须做本地用户验证（指纹/面容/系统 PIN），否则只是"点一下确认"，
                    // 达不到第二因子的强度（知识因子仍是密码）
                    UserVerification = UserVerificationRequirement.Required
                },
                AttestationPreference = AttestationConveyancePreference.None,
                Extensions = new AuthenticationExtensionsClientInputs { CredProps = true }
            });

            return SaveChallenge(options.ToJson(), options.Challenge, new WebAuthnChallengePayload
            {
                UserId = userId,
                TenantId = App.GetCurrentTenantId(),
                Type = WebAuthnChallengeType.Register
            }, settings);
        }

        public async Task<TwoFactorWebAuthnCredentialDto> CompleteRegistrationAsync(long userId, string challenge, string attestationJson, string deviceName)
        {
            if (!IsAvailable) throw new CustomException("当前系统未启用 Passkey，请联系管理员");

            var settings = Settings;
            var payload = CacheService.ConsumeWebAuthnChallenge<WebAuthnChallengePayload>(challenge, ChallengeMinutes(settings));
            if (payload == null) throw new CustomException("验证已超时，请重新发起绑定");
            if (payload.UserId != userId || payload.Type != WebAuthnChallengeType.Register)
            {
                throw new CustomException("验证信息不匹配，请重新发起绑定");
            }

            var attestation = ParseAttestation(attestationJson);

            CredentialCreateOptions options;
            try
            {
                options = CredentialCreateOptions.FromJson(payload.OptionsJson);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"WebAuthn 注册选项解析失败：UserId={userId}");
                throw new CustomException("验证已失效，请重新发起绑定");
            }

            var verified = await VerifyAttestationAsync(settings, attestation, options, userId);

            var credentialId = Base64UrlEncode(verified.Id);
            if (credentialId.Length > MaxCredentialIdLength)
            {
                // 规范允许到 1023 字节，但本项目列宽 512；超长直接拒绝而不是静默截断（截断即损坏）
                throw new CustomException("认证器返回的凭据 ID 过长，暂不支持");
            }

            var now = DateTime.Now;
            var entity = new SysUserWebAuthnCredential
            {
                UserId = userId,
                CredentialId = credentialId,
                PublicKey = Convert.ToBase64String(verified.PublicKey),
                UserHandle = Base64UrlEncode(Encoding.UTF8.GetBytes(userId.ToString())),
                // 注册时的计数器原样落库：同步型凭据恒为 0，后续认证的单调性校验由库负责
                SignCount = verified.SignCount,
                AaGuid = verified.AaGuid == Guid.Empty ? null : verified.AaGuid.ToString(),
                Transports = verified.Transports,
                IsBackupEligible = verified.IsBackupEligible ? 1 : 0,
                IsBackedUp = verified.IsBackedUp ? 1 : 0,
                DeviceName = Truncate(string.IsNullOrWhiteSpace(deviceName) ? "Passkey" : deviceName.Trim(), MaxDeviceNameLength),
                CreatedAt = now,
                LastUsedAt = now
            };

            // 写入数据库前再查一次数量：options 阶段与本次写入之间可能隔几十秒（用户在按指纹、等系统弹窗），
            // 期间完全可能在另一个标签页完成另一次绑定。
            //
            // 这里不引入事务/行锁：上限只是防刷表，不是安全边界，多一两条没有实际危害；
            // 加锁会让"绑定"这个低频操作去争抢数据库锁，收益远小于成本。
            var max = MaxCredentials(settings);
            var current = Context.Queryable<SysUserWebAuthnCredential>().Count(it => it.UserId == userId);
            if (current >= max)
            {
                throw new CustomException($"最多绑定 {max} 个 Passkey，请先删除不用的凭据");
            }

            Insert(entity);

            logger.Warn($"Passkey 已绑定：UserId={userId}, UserName={GetUser(userId)?.UserName}, 凭据Id={entity.Id}, AaGuid={entity.AaGuid}");

            return ToDto(entity);
        }

        public TwoFactorWebAuthnOptionsDto MakeAssertionOptions(long userId, string ticket)
        {
            if (!IsAvailable) throw new CustomException("当前系统未启用 Passkey，请联系管理员");
            if (string.IsNullOrWhiteSpace(ticket)) throw new CustomException("请求参数错误");

            var settings = Settings;

            // 只允许该账号已绑定的凭据：AllowCredentials 是服务端下发的白名单，
            // 浏览器不会把其他账号的凭据送进来，服务端再按用户校验一次
            var credentials = Context.Queryable<SysUserWebAuthnCredential>()
                .Where(it => it.UserId == userId)
                .ToList();

            if (credentials.Count == 0) throw new CustomException("当前账号尚未绑定 Passkey");

            var options = BuildFido2(settings).GetAssertionOptions(new GetAssertionOptionsParams
            {
                AllowedCredentials = credentials
                    .Select(c => new PublicKeyCredentialDescriptor(Base64UrlDecode(c.CredentialId)))
                    .ToList(),
                // 与注册保持一致：必须做本地用户验证
                UserVerification = UserVerificationRequirement.Required
            });

            return SaveChallenge(options.ToJson(), options.Challenge, new WebAuthnChallengePayload
            {
                UserId = userId,
                TenantId = App.GetCurrentTenantId(),
                Type = WebAuthnChallengeType.Authenticate,
                // 绑定票据：防止拿 A 次登录的 challenge 去完成 B 次登录
                Ticket = ticket
            }, settings);
        }

        public async Task<long> VerifyAssertionAsync(WebAuthnChallengePayload payload, string assertionJson)
        {
            if (!IsAvailable) throw new CustomException("当前系统未启用 Passkey，请联系管理员");
            if (payload == null) throw new CustomException("验证已超时，请重新登录");

            var assertion = ParseAssertion(assertionJson);

            // 断言里的 rawId 是认证器签名时使用的凭据，按它定位具体记录；
            // 查询条件带 UserId，防止用他人凭据 ID 冒充
            var credentialId = Base64UrlEncode(assertion.RawId);
            var entity = Context.Queryable<SysUserWebAuthnCredential>()
                .Where(it => it.UserId == payload.UserId && it.CredentialId == credentialId)
                .First();

            if (entity == null) throw new CustomException("凭据不存在或已解绑");

            AssertionOptions options;
            try
            {
                options = AssertionOptions.FromJson(payload.OptionsJson);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"WebAuthn 认证选项解析失败：UserId={payload.UserId}");
                throw new CustomException("验证已失效，请重新登录");
            }

            var verified = await VerifyAssertionCoreAsync(payload.UserId, entity.Id, assertion, options, entity);

            // CAS 更新：并发提交同一断言时只有一个能把计数器推上去。
            // 同步型凭据的计数器恒为 0，此时条件恒成立，防重放由 challenge 一次性消费负责。
            var oldCount = entity.SignCount;
            var newCount = verified.SignCount;
            var now = DateTime.Now;

            var affected = Context.Updateable<SysUserWebAuthnCredential>()
                .SetColumns(it => it.SignCount == newCount)
                .SetColumns(it => it.LastUsedAt == now)
                .Where(it => it.Id == entity.Id && it.SignCount == oldCount)
                .ExecuteCommand();

            if (affected == 0)
            {
                logger.Warn($"WebAuthn 计数器更新冲突（疑似并发重放）：UserId={payload.UserId}, 凭据Id={entity.Id}");
                throw new CustomException("验证冲突，请重新登录");
            }

            return entity.Id;
        }

        public List<TwoFactorWebAuthnCredentialDto> GetCredentials(long userId)
        {
            // 内存映射而非在 Select 里做 bool 转换：跨数据库方言对表达式里的类型转换支持不一致
            var list = Context.Queryable<SysUserWebAuthnCredential>()
                .Where(it => it.UserId == userId)
                .OrderBy(it => it.LastUsedAt, OrderByType.Desc)
                .ToList();

            return list.Select(ToDto).ToList();
        }

        public bool RemoveCredential(long userId, long credentialId)
        {
            // 条件带 UserId：防止用他人凭据 Id 越权删除
            var affected = Context.Deleteable<SysUserWebAuthnCredential>()
                .Where(it => it.Id == credentialId && it.UserId == userId)
                .ExecuteCommand();

            if (affected > 0)
            {
                logger.Warn($"Passkey 已解绑：UserId={userId}, 凭据Id={credentialId}");
            }

            return affected > 0;
        }

        public bool RenameCredential(long userId, long credentialId, string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName)) throw new CustomException("设备名称不能为空");

            var name = Truncate(deviceName.Trim(), MaxDeviceNameLength);

            return Context.Updateable<SysUserWebAuthnCredential>()
                .SetColumns(it => it.DeviceName == name)
                .Where(it => it.Id == credentialId && it.UserId == userId)
                .ExecuteCommand() > 0;
        }

        public int RemoveAllCredentials(long userId)
        {
            var affected = Context.Deleteable<SysUserWebAuthnCredential>()
                .Where(it => it.UserId == userId)
                .ExecuteCommand();

            if (affected > 0)
            {
                logger.Warn($"Passkey 凭据已全部清空：UserId={userId}, 条数={affected}");
            }

            return affected;
        }

        #region 内部方法

        /// <summary>
        /// 校验 attestation。
        /// 返回元组而非库的返回类型：该返回类型名在小版本间变过（MakeNewCredentialResult → VerifyAttestationResult），
        /// 把耦合收在这一个方法里，库升级时只需改这里。
        /// </summary>
        private async Task<(byte[] Id, byte[] PublicKey, long SignCount, Guid AaGuid, string Transports, bool IsBackupEligible, bool IsBackedUp)> VerifyAttestationAsync(
            TwoFactorSettings settings, AuthenticatorAttestationRawResponse attestation,
            CredentialCreateOptions options, long userId)
        {
            try
            {
                var result = await BuildFido2(settings).MakeNewCredentialAsync(new MakeNewCredentialParams
                {
                    AttestationResponse = attestation,
                    OriginalOptions = options,
                    IsCredentialIdUniqueToUserCallback = (args, cancellationToken) =>
                    {
                        // 全局唯一：凭据 ID 由认证器生成，重复意味着同一条凭据被重复注册
                        var id = Base64UrlEncode(args.CredentialId);
                        var exist = Context.Queryable<SysUserWebAuthnCredential>()
                            .Any(it => it.CredentialId == id);
                        return Task.FromResult(!exist);
                    }
                });

                return (result.Id, result.PublicKey, result.SignCount, result.AaGuid,
                    JoinTransports(result.Transports), result.IsBackupEligible, result.IsBackedUp);
            }
            catch (Fido2VerificationException ex)
            {
                logger.Warn($"WebAuthn 注册校验失败：UserId={userId}, 原因={ex.Message}");
                throw new CustomException("Passkey 校验失败，请重试");
            }
        }

        /// <summary>
        /// 校验断言。计数器回退（疑似克隆）由库判定并抛 <see cref="Fido2VerificationException"/>，
        /// 这里统一转成业务异常并留痕 —— 库异常直接冒泡会被全局处理成"系统异常"，丢掉安全语义。
        /// </summary>
        private async Task<(byte[] CredentialId, long SignCount)> VerifyAssertionCoreAsync(
            long userId, long entityId, AuthenticatorAssertionRawResponse assertion,
            AssertionOptions options, SysUserWebAuthnCredential entity)
        {
            try
            {
                var result = await BuildFido2(Settings).MakeAssertionAsync(new MakeAssertionParams
                {
                    AssertionResponse = assertion,
                    OriginalOptions = options,
                    StoredPublicKey = Convert.FromBase64String(entity.PublicKey),
                    StoredSignatureCounter = (uint)entity.SignCount,
                    // 不能无条件放行：当前安全性还依赖上游"按 UserId + CredentialId 查库"这个不变量，
                    // 而那是另一个函数里的隐式约束 —— 谁改了查询条件都不会有人发现。
                    // 这里显式校验一次，把归属判断放回规范要求的挂载点。
                    IsUserHandleOwnerOfCredentialIdCallback = (args, cancellationToken) =>
                    {
                        if (!Base64UrlEncode(args.CredentialId).Equals(entity.CredentialId, StringComparison.Ordinal))
                        {
                            logger.Warn($"WebAuthn 断言凭据不匹配：UserId={userId}, 期望={entity.CredentialId}, 实际={Base64UrlEncode(args.CredentialId)}");
                            return Task.FromResult(false);
                        }

                        // 非 discoverable 凭据（如安全密钥）不带 userHandle，无从比对，按通过处理；
                        // 一刀切拒绝会让这类认证器完全无法登录
                        if (args.UserHandle == null || args.UserHandle.Length == 0)
                        {
                            return Task.FromResult(true);
                        }

                        var expected = Base64UrlDecode(entity.UserHandle);
                        return Task.FromResult(expected.Length > 0 && args.UserHandle.SequenceEqual(expected));
                    }
                });

                return (result.CredentialId, result.SignCount);
            }
            catch (Fido2VerificationException ex)
            {
                logger.Error($"WebAuthn 断言校验失败：UserId={userId}, 凭据Id={entityId}, 原因={ex.Message}");
                throw new CustomException("Passkey 验证失败，请重试");
            }
        }

        private TwoFactorWebAuthnOptionsDto SaveChallenge(string optionsJson, byte[] challengeBytes,
            WebAuthnChallengePayload payload, TwoFactorSettings settings)
        {
            var challenge = Base64UrlEncode(challengeBytes);
            payload.OptionsJson = optionsJson;

            CacheService.SetWebAuthnChallenge(challenge, payload, ChallengeMinutes(settings));

            return new TwoFactorWebAuthnOptionsDto
            {
                Challenge = challenge,
                OptionsJson = optionsJson
            };
        }

        private static Fido2 BuildFido2(TwoFactorSettings settings)
        {
            var rpId = GetRpId(settings);
            if (string.IsNullOrEmpty(rpId))
            {
                throw new CustomException("Passkey 未配置域名，请联系管理员");
            }

            var allOrigins = GetOrigins(settings);
            var origins = GetUsableOrigins(settings, rpId);

            if (origins.Count == 0)
            {
                // 最常见的坑：Origin 配 127.0.0.1 而 RPID 配 localhost —— 两者是不同的 RP ID，浏览器会直接拒绝。
                // 浏览器在这种情况下不给任何可读错误，所以必须在服务端说清楚，否则用户只看到"点了没反应"。
                logger.Error($"WebAuthn 配置不可用：RPID={rpId} 无法覆盖任何 Origin，Origins={string.Join(" | ", allOrigins)}");
                throw new CustomException("Passkey 域名配置有误，请联系管理员");
            }

            if (origins.Count != allOrigins.Count)
            {
                var ignored = allOrigins.Except(origins, StringComparer.OrdinalIgnoreCase);
                logger.Warn($"WebAuthn 部分 Origin 与 RPID 不匹配，已忽略：RPID={rpId}, 忽略={string.Join(" | ", ignored)}");
            }

            var config = new Fido2Configuration
            {
                // 4.3.1 起 ServerDomain/ServerName 已过时，改用 RPID/RPName（前者将在下个大版本移除）
                RPID = rpId,
                RPName = string.IsNullOrWhiteSpace(settings.WebAuthnRpName) ? settings.Issuer : settings.WebAuthnRpName,
                Origins = new HashSet<string>(origins, StringComparer.OrdinalIgnoreCase)
            };

            return new Fido2(config);
        }

        /// <summary>
        /// 规范化并校验 Origin 配置。
        ///
        /// 用 Uri 解析而不是字符串前缀匹配：前缀匹配会把 http://localhost.evil.com 当成回环地址放行，
        /// 也识别不出"配置里带了路径"这类永远匹配不上的写法（浏览器上报的 origin 只有 scheme + host + port）。
        ///
        /// 非 HTTPS 仅允许环回地址（localhost / 127.0.0.1 / ::1），这是 WebAuthn 规范规定的开发期例外。
        ///
        /// 本方法刻意不打日志：它会被 IsAvailable 在每次登录时调用，配置问题的提示统一由 BuildFido2 输出。
        /// </summary>
        private static List<string> GetOrigins(TwoFactorSettings settings)
        {
            var result = new List<string>();
            if (settings?.WebAuthnOrigins == null) return result;

            foreach (var raw in settings.WebAuthnOrigins)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;

                if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri))
                {
                    continue;
                }

                // 浏览器上报的 origin 不含路径 / 查询 / 片段，带这些的配置不可能匹配成功
                if (uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                {
                    continue;
                }

                var isHttps = uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
                var isLoopbackHttp = uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    && IsLoopbackHost(uri.Host);

                if (!isHttps && !isLoopbackHttp)
                {
                    continue;
                }

                // GetLeftPart 会规范化大小写与默认端口，避免 http://LOCALHOST:8887 这类写法匹配不上
                var origin = uri.GetLeftPart(UriPartial.Authority);

                if (!result.Contains(origin, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(origin);
                }
            }

            return result;
        }

        /// <summary>
        /// 过滤出能被 RP ID 覆盖的 Origin。
        ///
        /// RP ID 必须是 origin host 的可注册域后缀（相等，或以 ".{rpId}" 结尾），这是浏览器的硬性规则；
        /// 典型反例：Origin 配 http://127.0.0.1:8887 而 RPID 配 localhost —— 两者是不同的 RP ID，必然失败。
        /// </summary>
        private static List<string> GetUsableOrigins(TwoFactorSettings settings, string rpId)
        {
            var usable = new List<string>();
            if (string.IsNullOrEmpty(rpId)) return usable;

            foreach (var origin in GetOrigins(settings))
            {
                if (IsRpIdSuffixOf(rpId, new Uri(origin).Host))
                {
                    usable.Add(origin);
                }
            }

            return usable;
        }

        /// <summary>
        /// RP ID 是否为 host 的可注册域后缀：相等，或以 ".{rpId}" 结尾。
        /// 注意不能只判断 EndsWith(rpId)：那样 "evil-localhost" 会被 "localhost" 命中。
        /// </summary>
        private static bool IsRpIdSuffixOf(string rpId, string host)
        {
            if (string.IsNullOrEmpty(rpId) || string.IsNullOrEmpty(host)) return false;

            if (host.Equals(rpId, StringComparison.OrdinalIgnoreCase)) return true;

            return host.EndsWith("." + rpId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 是否环回地址。必须精确匹配而不是 StartsWith：
        /// 否则 localhost.evil.com 会被当作回环地址，从而在非 HTTPS 下被放行（虽然是精确比对不构成实际漏洞，
        /// 但配置意图会变得不可读，且校验规则会随实现细节漂移）。
        /// </summary>
        private static bool IsLoopbackHost(string host)
        {
            if (string.IsNullOrEmpty(host)) return false;

            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;

            // Uri.Host 对 IPv6 带方括号（如 [::1]），解析前先去掉
            var normalized = host.Trim('[', ']');
            return System.Net.IPAddress.TryParse(normalized, out var ip) && System.Net.IPAddress.IsLoopback(ip);
        }

        private static string GetRpId(TwoFactorSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.WebAuthnRpId))
            {
                return settings.WebAuthnRpId.Trim().TrimEnd('.');
            }

            // 未显式配置时取第一个合法 Origin 的 host。
            // 127.0.0.1 与 localhost 是两个不同的 RP ID，不能互相替代 —— 用哪个访问就必须配哪个。
            var origin = GetOrigins(settings).FirstOrDefault();
            return origin == null ? null : new Uri(origin).Host;
        }

        private static int ChallengeMinutes(TwoFactorSettings settings)
        {
            return settings.WebAuthnChallengeMinutes > 0 ? settings.WebAuthnChallengeMinutes : 5;
        }

        private static int MaxCredentials(TwoFactorSettings settings)
        {
            return settings.WebAuthnMaxCredentials > 0 ? settings.WebAuthnMaxCredentials : 10;
        }

        private SysUser GetUser(long userId)
        {
            return Context.Queryable<SysUser>().ClearFilter().Where(it => it.UserId == userId).First();
        }

        /// <summary>
        /// 密码二次校验。个人中心传明文、其它链路可能传 MD5，两种都接受（与现有 2FA 同口径）。
        /// </summary>
        private static void VerifyPassword(SysUser user, string password)
        {
            if (string.IsNullOrEmpty(password)) throw new CustomException("密码不能为空");

            var md5 = password.Length == 32 ? password : NETCore.Encrypt.EncryptProvider.Md5(password);
            if (!string.Equals(user.Password, md5, StringComparison.OrdinalIgnoreCase))
            {
                throw new CustomException("密码错误");
            }
        }

        /// <summary>
        /// 解析前端提交的 PublicKeyCredential（注册）。
        ///
        /// 刻意**不用** JsonConvert/JsonSerializer 直接反序列化成库的类型：
        /// 库用的是 System.Text.Json 契约（JsonPropertyName / JsonStringEnumMemberName，以及 byte[] 上的
        /// Base64URL 转换器），Newtonsoft 不认这些特性，会退化成普通 base64 解码；
        /// 而浏览器的 rawId / attestationObject 是 base64url（可能含 - 和 _），必然抛 FormatException。
        ///
        /// 这里只取需要的字段、自己解码后构造对象，行为完全可控，也不受库内部序列化配置变化影响。
        /// </summary>
        private static AuthenticatorAttestationRawResponse ParseAttestation(string json)
        {
            var root = ParseJsonObject(json);
            var response = root["response"] as JObject;
            if (response == null) throw new CustomException("凭据数据异常");

            var rawId = Base64UrlDecode(root.Value<string>("rawId"));
            var attestationObject = Base64UrlDecode(response.Value<string>("attestationObject"));
            var clientDataJson = Base64UrlDecode(response.Value<string>("clientDataJSON"));

            if (rawId.Length == 0 || attestationObject.Length == 0 || clientDataJson.Length == 0)
            {
                throw new CustomException("凭据数据不完整，请重试");
            }

            return new AuthenticatorAttestationRawResponse
            {
                Id = root.Value<string>("id"),
                RawId = rawId,
                // 网页端只会送 public-key，库的枚举里也只有它是有效值
                Type = PublicKeyCredentialType.PublicKey,
                Response = new AuthenticatorAttestationRawResponse.AttestationResponse
                {
                    AttestationObject = attestationObject,
                    ClientDataJson = clientDataJson,
                    Transports = ParseTransports(response["transports"])
                },
                // 库把该字段标为必填；这里给空对象，credProps 等扩展只影响展示，不参与校验
                ClientExtensionResults = new AuthenticationExtensionsClientOutputs()
            };
        }

        /// <summary>
        /// 解析前端提交的 PublicKeyCredential（认证断言）。不做库类型的直接反序列化，原因同 <see cref="ParseAttestation"/>。
        /// </summary>
        private static AuthenticatorAssertionRawResponse ParseAssertion(string json)
        {
            var root = ParseJsonObject(json);
            var response = root["response"] as JObject;
            if (response == null) throw new CustomException("凭据数据异常");

            var rawId = Base64UrlDecode(root.Value<string>("rawId"));
            var authenticatorData = Base64UrlDecode(response.Value<string>("authenticatorData"));
            var signature = Base64UrlDecode(response.Value<string>("signature"));
            var clientDataJson = Base64UrlDecode(response.Value<string>("clientDataJSON"));

            if (rawId.Length == 0 || authenticatorData.Length == 0
                || signature.Length == 0 || clientDataJson.Length == 0)
            {
                throw new CustomException("凭据数据不完整，请重试");
            }

            var userHandle = response.Value<string>("userHandle");

            return new AuthenticatorAssertionRawResponse
            {
                Id = root.Value<string>("id"),
                RawId = rawId,
                Type = PublicKeyCredentialType.PublicKey,
                Response = new AuthenticatorAssertionRawResponse.AssertionResponse
                {
                    AuthenticatorData = authenticatorData,
                    Signature = signature,
                    ClientDataJson = clientDataJson,
                    // 非 discoverable 凭据不带 userHandle，null 是合法值
                    UserHandle = string.IsNullOrEmpty(userHandle) ? null : Base64UrlDecode(userHandle)
                },
                ClientExtensionResults = new AuthenticationExtensionsClientOutputs()
            };
        }

        private static JObject ParseJsonObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new CustomException("凭据数据异常");
            if (json.Length > MaxAssertionJsonLength)
            {
                // 正常断言约 1~3 KB，超过量级必是异常构造，先拒绝避免反序列化被撑爆
                throw new CustomException("凭据数据异常");
            }

            try
            {
                return JObject.Parse(json);
            }
            catch (Exception ex)
            {
                logger.Warn($"WebAuthn 凭据 JSON 解析失败：{ex.Message}");
                throw new CustomException("凭据数据异常");
            }
        }

        /// <summary>
        /// 传输方式只用于展示，未知值直接忽略而不是报错 ——
        /// 浏览器可能上报新值，不该因此让整个认证失败。
        /// </summary>
        private static AuthenticatorTransport[] ParseTransports(JToken token)
        {
            var list = new List<AuthenticatorTransport>();
            if (token is JArray array)
            {
                foreach (var item in array)
                {
                    switch (item?.ToString()?.ToLowerInvariant())
                    {
                        case "usb": list.Add(AuthenticatorTransport.Usb); break;
                        case "nfc": list.Add(AuthenticatorTransport.Nfc); break;
                        case "ble": list.Add(AuthenticatorTransport.Ble); break;
                        case "smart-card": list.Add(AuthenticatorTransport.SmartCard); break;
                        case "hybrid": list.Add(AuthenticatorTransport.Hybrid); break;
                        case "internal": list.Add(AuthenticatorTransport.Internal); break;
                    }
                }
            }

            return list.ToArray();
        }

        private static TwoFactorWebAuthnCredentialDto ToDto(SysUserWebAuthnCredential entity)
        {
            return new TwoFactorWebAuthnCredentialDto
            {
                Id = entity.Id,
                DeviceName = entity.DeviceName,
                AaGuid = entity.AaGuid,
                IsBackupEligible = entity.IsBackupEligible == 1,
                IsBackedUp = entity.IsBackedUp == 1,
                CreatedAt = entity.CreatedAt,
                LastUsedAt = entity.LastUsedAt
            };
        }

        /// <summary>
        /// 把认证器上报的传输方式拼成逗号分隔文本（仅展示用）。
        /// 参数用非泛型 IEnumerable：库在不同版本里用数组或枚举集合，这样写不依赖具体元素类型。
        /// </summary>
        private static string JoinTransports(System.Collections.IEnumerable transports)
        {
            if (transports == null) return null;

            var items = new List<string>();
            foreach (var item in transports)
            {
                if (item != null) items.Add(item.ToString());
            }

            return items.Count == 0 ? null : Truncate(string.Join(",", items), 100);
        }

        private static string Base64UrlEncode(byte[] data)
        {
            if (data == null || data.Length == 0) return string.Empty;

            return Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private static byte[] Base64UrlDecode(string value)
        {
            if (string.IsNullOrEmpty(value)) return Array.Empty<byte>();

            var s = value.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }

            return Convert.FromBase64String(s);
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value[..maxLength];
        }

        #endregion
    }
}
