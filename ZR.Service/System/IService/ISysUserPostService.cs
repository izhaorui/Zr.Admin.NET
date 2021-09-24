using System.Collections.Generic;
using ZR.Model.System;

namespace ZR.Service.System.IService
{
    public interface ISysUserPostService: IBaseService<SysUserPost>
    {
        public void InsertUserPost(SysUser user);

        public List<long> GetUserPostsByUserId(long userId);

        public string GetPostsStrByUserId(long userId);
    }
}
