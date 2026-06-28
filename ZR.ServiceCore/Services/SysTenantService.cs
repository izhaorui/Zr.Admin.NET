using Infrastructure;
using Infrastructure.Attribute;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 租户服务
    /// </summary>
    [AppService(ServiceType = typeof(ISysTenantService), ServiceLifetime = LifeTime.Transient)]
    public class SysTenantService : BaseService<SysTenant>, ISysTenantService
    {
        public PagedInfo<SysTenant> GetPageList(SysTenantQueryDto parm)
        {
            var predicate = Expressionable.Create<SysTenant>();
            predicate = predicate.And(x => x.DelFlag == 0);
            predicate = predicate.AndIF(!string.IsNullOrWhiteSpace(parm.TenantId), x => x.TenantId.Contains(parm.TenantId));
            predicate = predicate.AndIF(!string.IsNullOrWhiteSpace(parm.TenantName), x => x.TenantName.Contains(parm.TenantName));
            predicate = predicate.AndIF(parm.Status != null, x => x.Status == parm.Status);
            predicate = predicate.AndIF(parm.BeginTime != null, x => x.Create_time >= parm.BeginTime);
            predicate = predicate.AndIF(parm.EndTime != null, x => x.Create_time <= parm.EndTime);

            return GetPages(predicate.ToExpression(), parm);
        }

        public string CheckTenantIdUnique(SysTenant tenant)
        {
            var tenantId = tenant?.Id ?? 0;
            var info = Queryable().First(x => x.TenantId == tenant.TenantId && x.DelFlag == 0);
            if (info != null && info.Id != tenantId)
            {
                return UserConstants.NOT_UNIQUE;
            }
            return UserConstants.UNIQUE;
        }

        public SysTenant GetByTenantId(string tenantId)
        {
            return Queryable().First(x => x.TenantId == tenantId && x.DelFlag == 0);
        }

        public void CheckTenant(string tenantId)
        {
            if (!App.IsTenantEnabled())
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new CustomException(ResultCode.LOGIN_ERROR, "租户标识不能为空", false);
            }

            var tenant = GetByTenantId(tenantId);
            if (tenant == null)
            {
                throw new CustomException(ResultCode.LOGIN_ERROR, "租户不存在", false);
            }

            if (tenant.Status == 1)
            {
                throw new CustomException(ResultCode.LOGIN_ERROR, "租户已停用", false);
            }

            if (tenant.ExpireTime.HasValue && tenant.ExpireTime.Value < DateTime.Now)
            {
                throw new CustomException(ResultCode.LOGIN_ERROR, "租户已过期", false);
            }
        }
    }
}
