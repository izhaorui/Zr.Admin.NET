using Infrastructure.Attribute;
using ZR.Model.System;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 用户岗位
    /// </summary>
    [AppService(ServiceType = typeof(ISysUserPostService), ServiceLifetime = LifeTime.Transient)]
    public class SysUserPostService : BaseService<SysUserPost>, ISysUserPostService
    {
        /// <summary>
        /// 新增用户岗位信息
        /// </summary>
        /// <param name="user"></param>
        public void InsertUserPost(SysUser user)
        {
            // 新增用户与岗位管理
            List<SysUserPost> list = new();
            foreach (var item in user.PostIds)
            {
                list.Add(new SysUserPost() { PostId = item, UserId = user.UserId });
            }
            InsertRange(list);
        }

        /// <summary>
        /// 查询用户岗位集合
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<long> GetUserPostsByUserId(long userId)
        {
            var list = GetList(f => f.UserId == userId);
            return list.Select(x => x.PostId).ToList();
        }

        /// <summary>
        /// 获取用户岗位
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public string GetPostsStrByUserId(long userId)
        {
            var list = SelectPostsByUserId(userId);
            return string.Join(',', list.Select(x => x.PostName));
        }

        public bool Delete(long userId)
        {
            return Delete(x => x.UserId == userId);
        }

        /// <summary>
        /// 获取用户岗位
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<SysPost> SelectPostsByUserId(long userId)
        {
            return Context.Queryable<SysPost, SysUserPost>((p, up) => new JoinQueryInfos(
                JoinType.Left, up.PostId == p.PostId
                )).Where((p, up) => up.UserId == userId)
                .Select<SysPost>().ToList();
        }
    }
}
