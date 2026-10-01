using System;
using System.Collections.Generic;
using Infrastructure.Model;

namespace Infrastructure.ModuleInit
{
    /// <summary>
    /// 业务模块初始化插件契约：模块自助注册"非菜单种子"与"菜单种子"。
    /// <para>
    /// 由 <c>ModuleInitRunner</c> 通过依赖注入发现全部实现，并按 <see cref="Order"/> 升序执行
    /// 「建表 → 非菜单种子 → 菜单种子」。框架层不再硬编码任何具体模块名，
    /// 新增业务模块只需在自己工程内实现本接口并标注
    /// <c>[AppService(ServiceType = typeof(IModuleInitProvider))]</c>，即自动纳入 <c>--initdb</c> 流程。
    /// </para>
    /// <para>
    /// 建表仍由同名的 <c>ITenantModuleInitializer</c> 负责，本契约只做"开关 + 种子"。
    /// </para>
    /// </summary>
    public interface IModuleInitProvider
    {
        /// <summary>
        /// 模块名。须与同名 <c>ITenantModuleInitializer.ModuleName</c> 保持一致
        /// （无建表器的纯菜单模块也使用该值做日志与跳过判断）。
        /// </summary>
        string ModuleName { get; }

        /// <summary>展示名（日志用）。</summary>
        string DisplayName { get; }

        /// <summary>
        /// 执行顺序（升序）。写菜单的模块必须排在消费菜单的模块之前
        /// （例如 Pro 的 AI 菜单必须先于 Saas 的默认套餐同步）。
        /// </summary>
        int Order { get; }

        /// <summary>该模块在 appsettings 的 ModuleInit 开关是否开启。</summary>
        bool IsEnabled(OptionsSetting options);

        /// <summary>
        /// 建表后可选执行的非菜单种子（如模块内置定时任务）。
        /// 无此类种子时无需实现（默认空实现）。
        /// </summary>
        void Seed() { }

        /// <summary>
        /// 菜单/按钮权限种子，返回日志行。
        /// 无菜单种子时无需实现（默认返回空集合）。
        /// </summary>
        List<string> SeedMenu() => new List<string>();
    }
}
