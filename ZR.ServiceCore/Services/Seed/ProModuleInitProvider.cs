using Infrastructure.Attribute;
using Infrastructure.Model;
using Infrastructure.ModuleInit;

namespace ZR.ServiceCore.Services.Seed
{
    /// <summary>
    /// 专业版(AI)模块初始化 Provider：data.xlsx 系统页下的 AI 能力按钮 +「AI治理 / 用量管理 / 我的用量 / AI 办公助手」菜单
    /// +「系统管理 - 应用接入」菜单（SSO 对外应用接入）。
    /// 属平台级功能（非业务模块），故种子逻辑保留在框架层，仅由 ModuleInitRunner 按 ModuleInit.Pro 开关统一调度。
    /// AI 功能本身的运行期开关由 AiOptions.Enable 控制，两者相互独立。
    /// </summary>
    [AppService(ServiceType = typeof(IModuleInitProvider))]
    public class ProModuleInitProvider : IModuleInitProvider
    {
        public string ModuleName => "Pro";

        public string DisplayName => "专业版(AI)";

        /// <summary>
        /// 必须早于 Saas(900)：Saas 的 EnsureTenantPlanMenuSeedData 会把"当时的非平台菜单"写进默认套餐，
        /// 若 AI 菜单尚未写入，首次 --initdb 时 AI 菜单进不了套餐（只能靠再次执行增量补上）。
        /// </summary>
        public int Order => 800;

        public bool IsEnabled(OptionsSetting options) => options.ModuleInit.Pro;

        /// <summary>
        /// 菜单/按钮权限种子：AI 能力按钮 + AI 应用页面 + 系统管理下的「应用接入」。
        /// 依赖 InitSeedData 已写入的系统菜单（由同一 --initdb 链路先行完成）。
        /// </summary>
        public List<string> SeedMenu()
        {
            var svc = new SystemMenuSeedService();
            return
            [
                svc.EnsureAiPermSeedData(),
                svc.EnsureAiUsageMenuSeedData(),
                svc.EnsureSsoAppMenuSeedData()
            ];
        }
    }
}
