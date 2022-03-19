using Infrastructure.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZR.Model.System;
using ZR.Repository.System;
using ZR.Service.System.IService;

namespace ZR.Service.System
{
    /// <summary>
    /// 用户岗位
    /// </summary>
    [AppService(ServiceType = typeof(ISysUserPostService), ServiceLifetime = LifeTime.Transient)]
    public class SysUserPostService : ISysUserPostService
    {
        private SysUserPostRepository UserPostRepository;
        public SysUserPostService(SysUserPostRepository userPostRepository)
        {
            UserPostRepository = userPostRepository;
        }

        /// <summary>
        /// 新增用户岗位信息
        /// </summary>
        /// <param name="user"></param>
        public void InsertUserPost(SysUser user)
        {
            // 新增用户与岗位管理
            List<SysUserPost> list = new List<SysUserPost>();
            foreach (var item in user.PostIds)
            {
                list.Add(new SysUserPost() { PostId = item, UserId = user.UserId });
            }
            UserPostRepository.Insert(list);
        }


        /// <summary>
        /// 查询用户岗位集合
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<long> GetUserPostsByUserId(long userId)
        {
            var list = UserPostRepository.GetList(f => f.UserId == userId);
            return list.Select(x => x.PostId).ToList();
        }

        /// <summary>
        /// 获取用户岗位
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public string GetPostsStrByUserId(long userId)
        {
            var list = UserPostRepository.SelectPostsByUserId(userId);
            return string.Join(',', list.Select(x => x.PostName));
        }

        public bool Delete(long userId)
        {
            return UserPostRepository.Delete(x => x.UserId == userId);
        }
    }
}
