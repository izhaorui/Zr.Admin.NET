using Infrastructure;
using Infrastructure.Attribute;
using ZR.Model;
using ZR.Model.System.Model;
using ZR.Model.System.Model.Dto;
using ZR.Repository;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 文件分组Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(ISysFileGroupService), ServiceLifetime = LifeTime.Transient)]
    public class SysFileGroupService : BaseService<SysFileGroup>, ISysFileGroupService
    {
        /// <summary>
        /// 查询文件分组列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<SysFileGroupDto> GetList(SysFileGroupQueryDto parm)
        {
            var predicate = QueryExp(parm);

            var response = Queryable()
                .Where(predicate.ToExpression())
                .ToPage<SysFileGroup, SysFileGroupDto>(parm);

            return response;
        }

        /// <summary>
        /// 查询文件分组树列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public List<SysFileGroup> GetTreeList(SysFileGroupQueryDto parm)
        {
            var predicate = Expressionable.Create<SysFileGroup>();

            var response = Queryable()
                .Where(predicate.ToExpression())
                .ToTree(it => it.Children, it => it.ParentId, 0);

            return response;
        }

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="GroupId"></param>
        /// <returns></returns>
        public SysFileGroup GetInfo(int GroupId)
        {
            var response = Queryable()
                .Where(x => x.GroupId == GroupId)
                .First();

            return response;
        }

        /// <summary>
        /// 添加文件分组
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public SysFileGroup AddSysFileGroup(SysFileGroup model)
        {
            var nameExist = Any(f => f.GroupName == model.GroupName && f.ParentId == model.ParentId);
            if (nameExist)
            {
                throw new CustomException($"名称[{model.GroupName}]已存在");
            }
            // INSERT 不经全局过滤，需手动填入租户ID
            model.TenantId = App.GetCurrentTenantId();
            return Insertable(model).ExecuteReturnEntity();
        }

        /// <summary>
        /// 修改文件分组
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public int UpdateSysFileGroup(SysFileGroup model)
        {
            return Update(model, true);
        }

        /// <summary>
        /// 查询导出表达式
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        private static Expressionable<SysFileGroup> QueryExp(SysFileGroupQueryDto parm)
        {
            var predicate = Expressionable.Create<SysFileGroup>();

            return predicate;
        }
    }
}