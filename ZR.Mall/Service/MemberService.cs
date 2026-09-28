using System.Text.RegularExpressions;
using Infrastructure.Extensions;
using ZR.Mall.Service.IService;
using ZR.Model.Member;
using ZR.Model.Models;
using ZR.ServiceCore.Services;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 通用会员 Service 业务层处理。
    /// 与后台 sys_user 完全隔离；首版仅支持手机号 + 短信验证码登录（登录即注册）。
    /// </summary>
    [AppService(ServiceType = typeof(IMemberService))]
    public class MemberService : BaseService<UmsMember>, IMemberService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 会员登录验证码用途编码（1登录 2注册 3找回密码 4查订单 5下单 6发货通知，7=会员登录）
        /// </summary>
        public const int MemberLoginSendType = 7;

        private const string PhonePattern = @"^1\d{10}$";

        private readonly ISmsCodeLogService _smsCodeLogService;

        public MemberService(ISmsCodeLogService smsCodeLogService)
        {
            _smsCodeLogService = smsCodeLogService;
        }

        /// <summary>
        /// 发送会员登录验证码
        /// </summary>
        public SmsCodeLog SendLoginCode(string phone, string ip)
        {
            ValidatePhone(phone);
            // 复用现有短信服务（含同手机号/同IP频率限制、落库留痕、验证码缓存）
            return _smsCodeLogService.AddSmscodeLog(new SmsCodeLog
            {
                PhoneNum = phone.ParseToLong(),
                SendType = MemberLoginSendType,
                UserIP = ip ?? string.Empty,
                Location = HttpContextExtension.GetIpInfo(ip)
            });
        }

        /// <summary>
        /// 手机号 + 验证码登录；未注册手机号自动注册为新会员
        /// </summary>
        public UmsMember LoginByPhone(string phone, string code, string ip)
        {
            ValidatePhone(phone);
            if (string.IsNullOrWhiteSpace(code) || !CacheService.CheckPhoneCode(phone, code))
            {
                throw new CustomException("验证码错误或已过期，请重新获取");
            }
            // 验证码一次性校验，防重放
            CacheService.RemovePhoneCode(phone);

            var member = Queryable().First(x => x.Phone == phone && x.IsDelete == 0)
                ?? CreateMember(phone);

            if (member.Status != 0)
            {
                throw new CustomException("账号已被禁用，请联系客服");
            }

            var now = DateTime.Now;
            Context.Updateable<UmsMember>()
                .SetColumns(it => new UmsMember { LastLoginTime = now, LastLoginIp = ip })
                .Where(it => it.Id == member.Id)
                .ExecuteCommand();

            member.LastLoginTime = now;
            member.LastLoginIp = ip;
            return member;
        }

        /// <summary>
        /// 更新会员资料（昵称/头像，仅更新传入的非空字段）
        /// </summary>
        public int UpdateProfile(long memberId, string nickName, string avatar)
        {
            if (memberId <= 0)
            {
                throw new CustomException("会员身份无效");
            }

            var update = Context.Updateable<UmsMember>();
            var hasChange = false;

            if (nickName != null)
            {
                nickName = nickName.Trim();
                if (nickName.Length == 0)
                {
                    throw new CustomException("昵称不能为空");
                }
                if (nickName.Length > 50)
                {
                    throw new CustomException("昵称长度不能超过50个字符");
                }
                update = update.SetColumns(it => it.NickName == nickName);
                hasChange = true;
            }

            if (avatar != null)
            {
                avatar = avatar.Trim();
                if (avatar.Length > 255)
                {
                    throw new CustomException("头像地址过长");
                }
                update = update.SetColumns(it => it.Avatar == avatar);
                hasChange = true;
            }

            if (!hasChange)
            {
                return 0;
            }

            return update.Where(it => it.Id == memberId && it.IsDelete == 0).ExecuteCommand();
        }

        /// <summary>
        /// 自动注册。并发下依赖 Phone 唯一索引兜底：插入冲突时回查既有会员，避免"先查后插"重复建号。
        /// </summary>
        private UmsMember CreateMember(string phone)
        {
            var entity = new UmsMember
            {
                Phone = phone,
                NickName = "会员" + MaskPhone(phone),
                RegisterTime = DateTime.Now,
                Status = 0,
                Gender = 0,
                IsDelete = 0
            };

            try
            {
                return Insertable(entity).ExecuteReturnEntity();
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"会员自动注册插入冲突，回查既有会员：{phone}");
                var exist = Queryable().First(x => x.Phone == phone && x.IsDelete == 0);
                if (exist == null)
                {
                    throw;
                }
                return exist;
            }
        }

        private static void ValidatePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone) || !Regex.IsMatch(phone, PhonePattern))
            {
                throw new CustomException("手机号格式不正确");
            }
        }

        /// <summary>
        /// 手机号脱敏（作为默认昵称，避免明文暴露）
        /// </summary>
        private static string MaskPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone) || phone.Length != 11)
            {
                return phone;
            }
            return phone.Substring(0, 3) + "****" + phone.Substring(7);
        }
    }
}
