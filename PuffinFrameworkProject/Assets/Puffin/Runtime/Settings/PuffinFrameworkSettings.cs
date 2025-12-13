using System.Collections.Generic;
using Puffin.Runtime.Core.Configs;
using UnityEngine;

namespace Puffin.Runtime.Settings
{
    /// <summary>
    /// 扫描模式
    /// </summary>
    public enum ScanMode
    {
        /// <summary>
        /// 扫描所有程序集（排除系统程序集）
        /// </summary>
        All,

        /// <summary>
        /// 只扫描指定的程序集
        /// </summary>
        Specified
    }

    /// <summary>
    /// 系统信息输出级别
    /// </summary>
    public enum SystemInfoLevel
    {
        None,
        Simple,
        Detailed
    }

    /// <summary>
    /// 编辑器语言
    /// </summary>
    public enum EditorLanguage
    {
        Chinese,
        English
    }

    /// <summary>
    /// 框架配置
    /// </summary>
    [SettingsPath("Puffinettings")]
    public class Puffinettings : SettingsBase<Puffinettings>
    {
        [Header("扫描配置")] [Tooltip("扫描模式")] public ScanMode scanMode = ScanMode.All;

        [Tooltip("是否只扫描带 [AutoRegister] 特性的系统")]
        public bool requireAutoRegister = true;

        [Tooltip("要扫描的程序集名称（ScanMode.Specified 时生效）")]
        public List<string> assemblyNames = new();

        [Tooltip("排除的程序集前缀")] public List<string> excludeAssemblyPrefixes = new()
        {
            "System",
            "Microsoft",
            "Unity",
            "mscorlib",
            "netstandard",
            "Mono",
            "nunit"
        };

        [Header("Runtime 配置")] [Tooltip("是否启用性能统计")]
        public bool enableProfiling;

        [Tooltip("预定义的条件符号")] public List<string> symbols = new();

        [Header("初始化配置")] [Tooltip("是否自动初始化（运行时进入 Play 模式自动初始化）")]
        public bool autoInitialize = true;

        [Tooltip("是否在编辑器模式下初始化支持 IEditorSupport 的系统")]
        public bool enableEditorSupport = true;

        [Header("日志配置")]
        [Tooltip("系统信息输出级别")]
        public SystemInfoLevel systemInfoLevel = SystemInfoLevel.Simple;

        [Header("编辑器配置")]
        [Tooltip("编辑器语言")]
        public EditorLanguage editorLanguage = EditorLanguage.Chinese;


        /// <summary>
        /// 转换为 ScannerConfig
        /// </summary>
        public ScannerConfig ToScannerConfig()
        {
            var config = new ScannerConfig
            {
                RequireAutoRegister = requireAutoRegister,
                ExcludeAssemblyPrefixes = new List<string>(excludeAssemblyPrefixes)
            };

            if (scanMode == ScanMode.Specified)
            {
                config.AssemblyPrefixes = new List<string>(assemblyNames);
            }

            return config;
        }

        /// <summary>
        /// 转换为 RuntimeConfig
        /// </summary>
        public RuntimeConfig ToRuntimeConfig()
        {
            return new RuntimeConfig
            {
                EnableProfiling = enableProfiling,
                Symbols = new List<string>(symbols)
            };
        }

    }
}