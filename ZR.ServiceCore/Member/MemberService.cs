using Infrastructure;
using Infrastructure.Attribute;
using SqlSugar;
using System.Text.RegularExpressions;
using ZR.Infrastructure.Helper;
using ZR.Model;
using ZR.Model.Member;
using ZR.Model.Member.Dto;
using ZR.Model.Models;
using ZR.Repository;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.Member
{
    /// <summary>
    /// 通用会员 Service 业务层处理。
    /// 与后台 sys_user 完全隔离；首版仅支持手机号 + 短信验证码登录（登录即注册）。
    /// 会员身份为平台级通用能力，置于框架共享层 ZR.ServiceCore，供各业务模块共用。
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
        /// 后台会员分页列表（手机号/昵称模糊、状态、注册时间区间筛选）。
        /// 与后台 sys_user 完全隔离，只读展示 + 状态管理，会员数据由 C 端注册产生。
        /// </summary>
        public PagedInfo<UmsMember> GetPageList(MemberQueryDto parm)
        {
            return Queryable()
                .Where(x => x.IsDelete == 0)
                .WhereIF(!string.IsNullOrWhiteSpace(parm.Phone), x => x.Phone.Contains(parm.Phone))
                .WhereIF(!string.IsNullOrWhiteSpace(parm.NickName), x => x.NickName.Contains(parm.NickName))
                .WhereIF(parm.Status != null, x => x.Status == parm.Status)
                .WhereIF(parm.BeginRegisterTime != null, x => x.RegisterTime >= parm.BeginRegisterTime)
                .WhereIF(parm.EndRegisterTime != null, x => x.RegisterTime <= parm.EndRegisterTime)
                // 前端未指定排序时按 Id 倒序（新会员在前）
                .OrderByIF(string.IsNullOrWhiteSpace(parm.Sort), x => x.Id, OrderByType.Desc)
                .ToPage(parm);
        }

        /// <summary>
        /// 后台启用/禁用会员。成功后立即清除状态缓存，
        /// 会员后续请求经 <see cref="MemberAuthorizeAttribute"/> 状态复核被拒绝（多实例下最迟一个缓存周期）。
        /// </summary>
        public int SetStatus(long memberId, int status)
        {
            if (memberId <= 0)
            {
                throw new CustomException("会员身份无效");
            }
            if (status != 0 && status != 1)
            {
                throw new CustomException("状态值不合法");
            }

            var rows = Context.Updateable<UmsMember>()
                .SetColumns(it => it.Status == status)
                .Where(it => it.Id == memberId && it.IsDelete == 0)
                .ExecuteCommand();
            if (rows > 0)
            {
                CacheService.RemoveMemberStatus(memberId);
            }
            return rows;
        }

        /// <summary>
        /// 会员是否可用（未禁用且未删除）。JWT 无状态，仅靠登录时查 Status 挡不住
        /// "先登录后被禁用，存量令牌在有效期内仍可调用"——本方法在过滤器里做状态复核。
        /// 状态走 60 秒缓存（Redis/内存由 CacheStore 按配置自动切换），SetStatus 时主动清除，
        /// 禁用指令多实例下最迟 60 秒全端生效；缓存未命中时回查一次库。
        /// </summary>
        public bool IsMemberActive(long memberId)
        {
            if (memberId <= 0)
            {
                return false;
            }

            var cached = CacheService.GetMemberStatus(memberId);
            if (cached.HasValue)
            {
                return cached.Value == 0;
            }

            var member = Queryable()
                .Where(x => x.Id == memberId)
                .Select(x => new UmsMember { Status = x.Status, IsDelete = x.IsDelete })
                .First();
            if (member == null)
            {
                return false;
            }

            var status = member.Status == 1 || member.IsDelete == 1 ? 1 : 0;
            CacheService.SetMemberStatus(memberId, status);
            return status == 0;
        }

        /// <summary>
        /// 自动注册。并发下依赖 Phone 唯一索引兜底：插入冲突时回查既有会员，避免"先查后插"重复建号。
        /// </summary>
        private UmsMember CreateMember(string phone)
        {
            var entity = new UmsMember
            {
                Phone = phone,
                // 手机号脱敏统一走 MaskUtil（AGENTS.md：手机号/姓名/证件/卡号/邮箱/IP 统一用 MaskUtil）
                NickName = "会员" + MaskUtil.MaskPhone(phone),
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
    }
}
