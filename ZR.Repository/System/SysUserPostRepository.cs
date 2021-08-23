using Infrastructure.Attribute;
using SqlSugar;
using System.Collections.Generic;
using ZR.Model.System;

namespace ZR.Repository.System
{
    /// <summary>
    /// 用户岗位
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class SysUserPostRepository : BaseRepository
    {
        /// <summary>
        /// 获取用户岗位
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<SysPost> SelectPostsByUserId(long userId)
        {
            return Db.Queryable<SysPost, SysUserPost>((p, up) => new JoinQueryInfos(
                JoinType.Left, up.PostId == p.PostId
                )).Where((p, up) => up.UserId == userId)
                .Select<SysPost>().ToList();
        }
    }
}
