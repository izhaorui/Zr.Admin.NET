using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Repository;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// SSO 应用接入管理
    /// </summary>
    public interface ISysSsoAppService : IBaseService<SysSsoApp>
    {
        /// <summary>
        /// 分页查询
        /// </summary>
        PagedInfo<SysSsoApp> GetPageList(SsoAppQueryDto parm);

        /// <summary>
        /// 创建应用：服务端生成 AppId 与密钥，密钥明文只在返回值中出现一次
        /// </summary>
        SsoAppSecretDto CreateApp(SsoAppEditDto dto);

        /// <summary>
        /// 修改应用（不改动密钥）
        /// </summary>
        int UpdateApp(SsoAppEditDto dto);

        /// <summary>
        /// 重置密钥：生成新密钥并返回明文一次，旧密钥立即失效
        /// </summary>
        SsoAppSecretDto ResetSecret(long id);

        /// <summary>
        /// 按应用标识查询
        /// </summary>
        SysSsoApp GetByAppId(string appId);

        /// <summary>
        /// 校验应用是否可用（存在、未停用、未过期；传密钥时一并校验密钥）。
        /// 任何一项不通过均返回 null —— 不向匿名调用方区分具体失败原因。
        /// </summary>
        SysSsoApp ValidateApp(string appId, string appSecret = null);

        /// <summary>
        /// 回调地址是否在应用白名单内（精确全等匹配）
        /// </summary>
        bool IsRedirectUriAllowed(SysSsoApp app, string redirectUri);

        /// <summary>
        /// 密钥哈希：SHA256(AppId:Secret)，入库与校验共用
        /// </summary>
        string HashSecret(string appId, string secret);
    }
}