using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Extensions;
using Infrastructure.Model;
using Microsoft.AspNetCore.Mvc;
using ZR.Model.Content.Dto;
using ZR.Model.social;
using ZR.Model.System;
using ZR.Repository;
using ZR.Service.Social.IService;

namespace ZR.Service.Social
{
    /// <summary>
    /// 粉丝
    /// </summary>
    [AppService(ServiceType = typeof(ISocialFansService))]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class SocialFansService : BaseService<SocialFans>, ISocialFansService, IDynamicApi
    {
        /// <summary>
        /// 查询关注/粉丝列表
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpGet]
        public ApiResult FollowList([FromQuery] FansQueryDto dto)
        {
            var userid = (int)HttpContextExtension.GetUId(App.HttpContext);
            PagedInfo<SocialFansDto> list = dto.SelectType == 1 ? GetFollow(dto, userid) : GetFans(dto, userid);

            return ApiResult.Success(list);
        }

        /// <summary>
        /// 是否关注
        /// </summary>
        /// <param name="toUserid"></param>
        /// <returns></returns>
        public ApiResult IsFollow([FromQuery] int toUserid)
        {
            var userid = (int)HttpContextExtension.GetUId(App.HttpContext);
            if (userid == toUserid || toUserid <= 0)
            {
                return ApiResult.Success("");
            }
            var isFollow = Any(f => f.Userid == userid && f.ToUserid == toUserid);

            return ApiResult.Success(isFollow);
        }

        /// <summary>
        /// 关注
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost]
        public ApiResult Follow([FromBody] SocialFansDto dto)
        {
            dto.Userid = (int)HttpContextExtension.GetUId(App.HttpContext);
            if (dto.Userid == dto.ToUserid)
            {
                throw new CustomException("不能关注自己");
            };
            if (dto.ToUserid <= 0)
            {
                return ApiResult.Error("关注失败");
            }
            //TODO 判断用户是否存在

            var isFollow = GetFirst(x => x.Userid == dto.Userid && x.ToUserid == dto.ToUserid);
            if (isFollow != null)
            {
                throw new CustomException("已关注");
            }
            var fans = new SocialFans
            {
                Userid = dto.Userid,
                ToUserid = dto.ToUserid,
                FollowTime = DateTime.Now,
                UserIP = HttpContextExtension.GetClientUserIp(App.HttpContext)
            };
            var result = UseTran2(() =>
            {
                //添加粉丝
                InsertReturnSnowflakeId(fans);

                UpdateFollowInfo(dto.Userid, isFollowNum: true);
                UpdateFollowInfo(dto.ToUserid, isFollowNum: false);
            });

            if (!result)
            {
                return ApiResult.Error("关注失败");
            }
            var data = Context.Queryable<SocialFansInfo>()
                    .Where(x => x.Userid == dto.Userid)
                    .First();
            return ApiResult.Success("关注成功", data);
        }

        /// <summary>
        /// 取消关注
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost]
        public ApiResult CancelFollow([FromBody] SocialFansDto dto)
        {
            dto.Userid = (int)HttpContextExtension.GetUId(App.HttpContext);
            var result = UseTran(() =>
            {
                //删除粉丝
                Delete(f => f.Userid == dto.Userid && f.ToUserid == dto.ToUserid);

                Context.Updateable<SocialFansInfo>()
                .SetColumns(x => x.FollowNum == x.FollowNum - 1)
                .Where(x => x.Userid == dto.Userid)
                .ExecuteCommand();

                Context.Updateable<SocialFansInfo>()
                .SetColumns(x => x.FansNum == x.FansNum - 1)
                .Where(x => x.Userid == dto.ToUserid)
                .ExecuteCommand();
            });

            if (!result.IsSuccess)
            {
                return ApiResult.Error("取消关注失败");
            }
            var data = Context.Queryable<SocialFansInfo>()
                    .Where(x => x.Userid == dto.Userid)
                    .First();
            return ApiResult.Success("取消关注成功", data);
        }

        private void UpdateFollowInfo(long userId, bool isFollowNum)
        {
            var count = Context.Updateable<SocialFansInfo>()
                .SetColumnsIF(isFollowNum, x => x.FollowNum == x.FollowNum + 1)
                .SetColumnsIF(!isFollowNum, x => x.FansNum == x.FansNum + 1)
                .Where(x => x.Userid == userId)
                .ExecuteCommand();

            if (count > 0) return;

            Context.Insertable(new SocialFansInfo
            {
                Userid = userId,
                FollowNum = isFollowNum ? 1 : 0,
                FansNum = isFollowNum ? 0 : 1,
                UpdateTime = DateTime.Now
            }).ExecuteCommand();
        }

        private PagedInfo<SocialFansDto> GetFollow(FansQueryDto dto, int userid)
        {
            return Queryable()
                            .LeftJoin<SysUser>((it, u) => it.ToUserid == u.UserId)
                            .Where(it => it.Userid == userid)
                            .Select((it, u) => new SocialFansDto()
                            {
                                User = new ArticleUser()
                                {
                                    Avatar = u.Avatar,
                                    NickName = u.NickName,
                                    Sex = u.Sex,
                                },
                                Status = 1,
                                ToUserid = it.ToUserid,
                            })
                            .ToPage(dto);
        }
        private PagedInfo<SocialFansDto> GetFans(FansQueryDto dto, int userid)
        {
            return Queryable()
                            .LeftJoin<SysUser>((it, u) => it.Userid == u.UserId)
                            .Where(it => it.ToUserid == userid)
                            .Select((it, u) => new SocialFansDto()
                            {
                                User = new ArticleUser()
                                {
                                    Avatar = u.Avatar,
                                    NickName = u.NickName,
                                    Sex = u.Sex,
                                },
                                Userid = it.Userid,
                            })
                            .ToPage(dto);
        }
    }
}
