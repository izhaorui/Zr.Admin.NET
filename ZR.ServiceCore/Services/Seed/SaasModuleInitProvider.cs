using Infrastructure.Attribute;
using Infrastructure.Model;
using Infrastructure.ModuleInit;

namespace ZR.ServiceCore.Services.Seed
{
    /// <summary>
    /// SaaS 模块初始化 Provider：平台级菜单与权限种子（租户管理、套餐菜单、字典种子等），无独立业务建表。
    /// 属平台级功能（非业务模块），故保留在框架层，由 ModuleInitRunner 按 ModuleInit.Saas 开关统一调度。
    /// </summary>
    [AppService(ServiceType = typeof(IModuleInitProvider))]
    public class SaasModuleInitProvider : IModuleInitProvider
    {
        public string ModuleName => "Saas";

        public string DisplayName => "专业版（SaaS）";

        /// <summary>晚于 Pro(800)：默认套餐同步会消费当时已写入的全部非平台菜单，故必须排在写菜单的模块之后。</summary>
        public int Order => 900;

        public bool IsEnabled(OptionsSetting options) => options.ModuleInit.Saas;

        /// <summary>
        /// 菜单/按钮权限种子：租户菜单、套餐菜单、字典种子（按固定顺序聚合）。
        /// </summary>
        public List<string> SeedMenu()
        {
            return new SaasMenuSeedService().EnsureAllSeedMenus();
        }
    }
}
