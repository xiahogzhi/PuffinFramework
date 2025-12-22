using Cysharp.Threading.Tasks;
using Puffin.Runtime.Core;

namespace Puffin.Boot.Runtime
{
    /// <summary>
    /// 启动器接口，用于在框架启动的不同阶段注入自定义逻辑
    /// 模块可以实现此接口来自定义启动流程，无需修改 Launcher 代码
    /// </summary>
    public interface IBootstrap
    {
        /// <summary>
        /// 优先级，数值越小越先执行（默认 0）
        /// 建议范围：
        /// - 资源系统配置: -1000
        /// - 热更新系统: -500
        /// - 普通模块: 0
        /// - 后处理: 1000
        /// </summary>
        int Priority => 0;

        /// <summary>
        /// 是否在编辑器模式下执行（默认 false）
        /// 设置为 true 时，Bootstrap 会在编辑器模式下也执行
        /// </summary>
        bool SupportEditorMode => false;

        /// <summary>
        /// 在 Setup 之前执行，用于配置 SetupContext
        /// 此阶段可以：
        /// - 配置自定义资源加载器
        /// - 配置自定义日志系统
        /// - 修改扫描配置
        /// </summary>
        /// <param name="context">Setup 上下文</param>
        UniTask OnPreSetup(SetupContext context);

        /// <summary>
        /// 在 Setup 之后、Start 之前执行
        /// 此阶段可以：
        /// - 执行热更新检查
        /// - 预加载必要资源
        /// - 初始化第三方 SDK
        /// </summary>
        UniTask OnPostSetup();

        /// <summary>
        /// 在框架 Start 之后执行
        /// 此阶段可以：
        /// - 执行启动后的初始化
        /// - 加载首个场景
        /// </summary>
        UniTask OnPostStart();
    }
}
