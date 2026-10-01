using Infrastructure.Attribute;
using Infrastructure.Model;
using Infrastructure.ModuleInit;

namespace ZR.ServiceCore.Member
{
    /// <summary>
    /// 会员模块初始化 Provider：仅承载 ModuleInit.Member 开关（驱动 MemberTenantInitializer 建 ums_member），
    /// 无菜单/任务种子（会员登录接口与后台会员菜单属运行期/后续迭代内容）。
    /// 会员身份为平台级通用能力（与 sys_user 对等），故与 Pro/Saas 一样保留在框架层。
    /// </summary>
    [AppService(ServiceType = typeof(IModuleInitProvider))]
    public class MemberModuleInitProvider : IModuleInitProvider
    {
        public string ModuleName => "Member";

        public string DisplayName => "会员";

        /// <summary>建表型模块，无菜单产出；置于 Workflow(200) 之后、Pro(800) 之前，与原字典顺序约束不冲突。</summary>
        public int Order => 300;

        public bool IsEnabled(OptionsSetting options) => options.ModuleInit.Member;
    }
}
