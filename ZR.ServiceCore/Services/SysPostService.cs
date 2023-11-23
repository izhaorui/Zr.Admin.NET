using Infrastructure.Attribute;
using ZR.Model.System;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 岗位管理
    /// </summary>
    [AppService(ServiceType = typeof(ISysPostService), ServiceLifetime = LifeTime.Transient)]
    public class SysPostService : BaseService<SysPost>, ISysPostService
    {
        /// <summary>
        /// 校验岗位编码是否唯一
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        public string CheckPostCodeUnique(SysPost post)
        {
            SysPost info = GetFirst(it => it.PostCode.Equals(post.PostCode));
            if (info != null && info.PostId != post.PostId)
            {
                return UserConstants.NOT_UNIQUE;
            }
            return UserConstants.UNIQUE;
        }

        /// <summary>
        /// 校验岗位名称是否唯一
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        public string CheckPostNameUnique(SysPost post)
        {
            SysPost info = GetFirst(it => it.PostName.Equals(post.PostName));
            if (info != null && info.PostId != post.PostId)
            {
                return UserConstants.NOT_UNIQUE;
            }
            return UserConstants.UNIQUE;
        }

        public List<SysPost> GetAll()
        {
            return GetAll(false);
        }
    }
}
