using Infrastructure.Model;
using Microsoft.AspNetCore.Mvc;
using ZR.Model.Content;
using ZR.Model.System.Dto;

namespace ZR.Service.IService
{
    /// <summary>
    /// Hello接口
    /// </summary>
    public interface IHelloService : IBaseService<ArticleCategory>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        string SayHello(string name);
        ApiResult SayHello2([FromBody] SysUserDto userDto);
        ApiResult SayHello3();
        ApiResult SayHelloJson([FromBody] SysUserDto userDto);
    }
}
