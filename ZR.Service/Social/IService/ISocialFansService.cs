using Infrastructure.Model;
using ZR.Model.social;

namespace ZR.Service.Social.IService
{
    public interface ISocialFansService : IBaseService<SocialFans>
    {
        /// <summary>
        /// 查询关注/粉丝列表
        /// </summary>
        ApiResult FollowList(FansQueryDto dto);

        /// <summary>
        /// 是否关注
        /// </summary>
        ApiResult IsFollow(int toUserid);

        /// <summary>
        /// 关注
        /// </summary>
        ApiResult Follow(SocialFansDto dto);

        /// <summary>
        /// 取消关注
        /// </summary>
        ApiResult CancelFollow(SocialFansDto dto);
    }
}
