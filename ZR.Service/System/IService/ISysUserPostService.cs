using System.Collections.Generic;
using ZR.Model.System;

namespace ZR.Service.System.IService
{
    public interface ISysUserPostService
    {
        public void InsertUserPost(SysUser user);

        public List<long> GetUserPostsByUserId(long userId);

        public string GetPostsStrByUserId(long userId);
        int Delete(long userId);
    }
}
