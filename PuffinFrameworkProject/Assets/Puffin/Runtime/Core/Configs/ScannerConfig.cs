using System;
using System.Collections.Generic;
using System.Reflection;

namespace Puffin.Runtime.Core.Configs
{
    /// <summary>
    /// 扫描器配置
    /// </summary>
    public class ScannerConfig
    {
        /// <summary>
        /// 是否只扫描带 [AutoRegister] 特性的系统
        /// </summary>
        public bool RequireAutoRegister { get; set; } = true;

        /// <summary>
        /// 要扫描的程序集（为空则扫描所有）
        /// </summary>
        public List<Assembly> Assemblies { get; set; } = new();

        /// <summary>
        /// 程序集名称过滤（包含这些前缀的才扫描）
        /// </summary>
        public List<string> AssemblyPrefixes { get; set; } = new();

        /// <summary>
        /// 排除的程序集名称前缀
        /// </summary>
        public List<string> ExcludeAssemblyPrefixes { get; set; } = new()
        {
            "System",
            "Microsoft",
            "Unity",
            "mscorlib",
            "netstandard"
        };

        /// <summary>
        /// 类型过滤器
        /// </summary>
        public Func<Type, bool> TypeFilter { get; set; }
    }
}
