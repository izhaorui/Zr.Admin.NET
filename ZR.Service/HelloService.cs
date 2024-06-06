using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar.IOC;
using ZR.Admin.WebApi.Filters;
using ZR.Model.Content;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Repository;
using ZR.Service.IService;
using ZR.ServiceCore.Services;

namespace ZR.Service
{
    /// <summary>
    /// 动态api示例，继承IDynamicApi，使用看swagger生成的地址
    /// </summary>
    [AppService(ServiceType = typeof(IHelloService), ServiceLifetime = LifeTime.Transient)]
    public class HelloService : BaseService<ArticleCategory>, IHelloService, IDynamicApi
    {
        /// <summary>
        /// 引用User服务
        /// </summary>
        private readonly ISysUserService userService;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userService"></param>
        public HelloService(ISysUserService userService)
        {
            this.userService = userService;
        }
        /// <summary>
        /// 数据库使用案例
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string SayHello(string name)
        {
            //构造函数式使用
            var user = JsonConvert.SerializeObject(userService.GetFirst(f => f.UserId == 1));
            Console.WriteLine(user);
            
            var postService = App.GetRequiredService<ISysPostService>();
            Console.WriteLine(JsonConvert.SerializeObject(postService.GetId(1)));

            BaseRepository<SysDept> deptRepo = new();
            Console.WriteLine(JsonConvert.SerializeObject(deptRepo.GetId(1)));

            var result = DbScoped.SugarScope.Queryable<SysDictType>().Where(f => f.DictId == 1).First();
            Console.WriteLine(JsonConvert.SerializeObject(result));

            //切换库
            //DbScoped.SugarScope.GetConnectionScope(2);

            GetFirst(x => x.CategoryId == 1);
            Context.Queryable<SysUser>().First(f => f.UserId == 1);

            return "Hello:" + name;
        }

        /// <summary>
        /// 返回json内容
        /// </summary>
        /// <param name="userDto"></param>
        /// <returns></returns>
        [Verify]
        public ApiResult SayHello2([FromBody]SysUserDto userDto)
        {
            var user = userService.GetFirst(f => f.UserId == 2);
            return new ApiResult(100, "success", user);
        }

        public ApiResult SayHello3()
        {
            throw new CustomException("自定义异常");
        }

        /// <summary>
        /// 返回json内容
        /// </summary>
        /// <param name="userDto"></param>
        /// <returns></returns>
        [AllowAnonymous]
        public ApiResult SayHelloJson([FromBody] SysUserDto userDto)
        {
            return new ApiResult(100, "success", userDto);
        }
    }
}
