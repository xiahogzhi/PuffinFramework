using Puffin.Runtime.Core.Configs;
using Puffin.Runtime.Interfaces;

namespace Puffin.Runtime.Core
{
    /// <summary>
    /// 框架初始化上下文，用于配置框架启动时的各项参数
    /// </summary>
    public class SetupContext
    {
        /// <summary>
        /// 资源加载器，用于加载游戏资源
        /// </summary>
        public IResourcesLoader ResourcesLoader { get; set; }

        /// <summary>
        /// 日志记录器，用于输出框架日志
        /// </summary>
        public IPuffinLogger Logger { get; set; }

        /// <summary>
        /// 系统扫描器配置，控制如何扫描和发现游戏系统
        /// </summary>
        public ScannerConfig ScannerConfig { set; get; }

        /// <summary>
        /// 运行时配置，包含性能统计、条件符号等设置
        /// </summary>
        public RuntimeConfig runtimeConfig { set; get; }
    }
}