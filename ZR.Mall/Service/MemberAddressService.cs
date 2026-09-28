using System.Text.RegularExpressions;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 会员收货地址 Service 业务层处理。
    /// 归属列固定为 MemberId，全部读写都带 memberId 条件，防止越权访问他人地址。
    /// </summary>
    [AppService(ServiceType = typeof(IMemberAddressService))]
    public class MemberAddressService : BaseService<MMSUserAddress>, IMemberAddressService
    {
        /// <summary>
        /// 单会员地址上限（防刷与展示体验）
        /// </summary>
        private const int MaxAddressPerMember = 20;

        private const string PhonePattern = @"^1\d{10}$";

        public List<MMSUserAddress> GetList(long memberId)
        {
            EnsureMember(memberId);
            return Queryable()
                .Where(x => x.MemberId == memberId)
                .OrderBy(x => x.IsDefault, OrderByType.Desc)
                .OrderBy(x => x.Id, OrderByType.Desc)
                .ToList();
        }

        public MMSUserAddress Add(long memberId, MemberAddressDto dto)
        {
            EnsureMember(memberId);
            Validate(dto);

            var count = Queryable().Where(x => x.MemberId == memberId).Count();
            if (count >= MaxAddressPerMember)
            {
                throw new CustomException($"收货地址最多 {MaxAddressPerMember} 条，请先删除部分地址");
            }

            // 首条地址自动为默认，避免下单时没有可用默认地址
            var makeDefault = dto.IsDefault == 1 || count == 0;
            var entity = new MMSUserAddress
            {
                MemberId = memberId,
                UserName = dto.UserName.Trim(),
                Phone = dto.Phone.Trim(),
                PhoneCode = dto.PhoneCode?.Trim(),
                Province = dto.Province.Trim(),
                City = dto.City.Trim(),
                District = dto.District.Trim(),
                DetailAddress = dto.DetailAddress.Trim(),
                IsDefault = makeDefault ? 1 : 0,
                AddTime = DateTime.Now
            };

            var tran = UseTran(() =>
            {
                if (makeDefault)
                {
                    ClearDefault(memberId);
                }
                entity.Id = Context.Insertable(entity).ExecuteReturnBigIdentity();
            });
            if (tran == null || !tran.IsSuccess)
            {
                throw new CustomException("新增收货地址失败，请重试");
            }
            return entity;
        }

        public int UpdateAddress(long memberId, MemberAddressDto dto)
        {
            EnsureMember(memberId);
            if (dto == null || dto.Id <= 0)
            {
                throw new CustomException("地址Id不能为空");
            }
            Validate(dto);

            var exist = Queryable().First(x => x.Id == dto.Id && x.MemberId == memberId);
            if (exist == null)
            {
                throw new CustomException("收货地址不存在");
            }

            var makeDefault = dto.IsDefault == 1;
            // 未传默认标记时保持原值，避免把已默认的地址改回非默认
            var targetDefault = makeDefault ? 1 : exist.IsDefault;
            var affected = 0;

            var tran = UseTran(() =>
            {
                if (makeDefault && exist.IsDefault != 1)
                {
                    ClearDefault(memberId);
                }
                affected = Context.Updateable<MMSUserAddress>()
                    .SetColumns(it => new MMSUserAddress
                    {
                        UserName = dto.UserName.Trim(),
                        Phone = dto.Phone.Trim(),
                        PhoneCode = dto.PhoneCode,
                        Province = dto.Province.Trim(),
                        City = dto.City.Trim(),
                        District = dto.District.Trim(),
                        DetailAddress = dto.DetailAddress.Trim(),
                        IsDefault = targetDefault
                    })
                    .Where(it => it.Id == dto.Id && it.MemberId == memberId)
                    .ExecuteCommand();
            });
            if (tran == null || !tran.IsSuccess)
            {
                throw new CustomException("更新收货地址失败，请重试");
            }
            return affected;
        }

        public int DeleteAddress(long memberId, long id)
        {
            EnsureMember(memberId);
            var exist = Queryable().First(x => x.Id == id && x.MemberId == memberId);
            if (exist == null)
            {
                throw new CustomException("收货地址不存在");
            }

            var affected = 0;
            var tran = UseTran(() =>
            {
                affected = Context.Deleteable<MMSUserAddress>()
                    .Where(it => it.Id == id && it.MemberId == memberId)
                    .ExecuteCommand();

                // 删除的是默认地址：把剩余最新一条补为默认，避免没有默认地址
                if (exist.IsDefault == 1)
                {
                    var next = Queryable()
                        .Where(x => x.MemberId == memberId)
                        .OrderBy(x => x.Id, OrderByType.Desc)
                        .First();
                    if (next != null)
                    {
                        Context.Updateable<MMSUserAddress>()
                            .SetColumns(it => it.IsDefault == 1)
                            .Where(it => it.Id == next.Id && it.MemberId == memberId)
                            .ExecuteCommand();
                    }
                }
            });
            if (tran == null || !tran.IsSuccess)
            {
                throw new CustomException("删除收货地址失败，请重试");
            }
            return affected;
        }

        public int SetDefault(long memberId, long id)
        {
            EnsureMember(memberId);
            var exist = Queryable().First(x => x.Id == id && x.MemberId == memberId);
            if (exist == null)
            {
                throw new CustomException("收货地址不存在");
            }
            if (exist.IsDefault == 1)
            {
                return 1;
            }

            var tran = UseTran(() =>
            {
                ClearDefault(memberId);
                Context.Updateable<MMSUserAddress>()
                    .SetColumns(it => it.IsDefault == 1)
                    .Where(it => it.Id == id && it.MemberId == memberId)
                    .ExecuteCommand();
            });
            if (tran == null || !tran.IsSuccess)
            {
                throw new CustomException("设置默认地址失败，请重试");
            }
            return 1;
        }

        /// <summary>
        /// 清除该会员的默认标记（事务内调用，随后设置新的默认，保证同会员仅一条默认）
        /// </summary>
        private void ClearDefault(long memberId)
        {
            Context.Updateable<MMSUserAddress>()
                .SetColumns(it => it.IsDefault == 0)
                .Where(it => it.MemberId == memberId && it.IsDefault == 1)
                .ExecuteCommand();
        }

        private static void EnsureMember(long memberId)
        {
            if (memberId <= 0)
            {
                throw new CustomException("会员身份无效");
            }
        }

        private static void Validate(MemberAddressDto dto)
        {
            if (dto == null)
            {
                throw new CustomException("参数不能为空");
            }
            if (string.IsNullOrWhiteSpace(dto.UserName))
            {
                throw new CustomException("收货人不能为空");
            }
            if (dto.UserName.Trim().Length > 50)
            {
                throw new CustomException("收货人姓名不能超过50个字符");
            }
            if (string.IsNullOrWhiteSpace(dto.Phone) || !Regex.IsMatch(dto.Phone.Trim(), PhonePattern))
            {
                throw new CustomException("手机号格式不正确");
            }
            if (string.IsNullOrWhiteSpace(dto.Province))
            {
                throw new CustomException("请选择所在省份");
            }
            if (string.IsNullOrWhiteSpace(dto.City))
            {
                throw new CustomException("请选择所在城市");
            }
            if (string.IsNullOrWhiteSpace(dto.District))
            {
                throw new CustomException("请选择所在区县");
            }
            if (string.IsNullOrWhiteSpace(dto.DetailAddress))
            {
                throw new CustomException("详细地址不能为空");
            }
            if (dto.DetailAddress.Trim().Length > 200)
            {
                throw new CustomException("详细地址不能超过200个字符");
            }
        }
    }
}
