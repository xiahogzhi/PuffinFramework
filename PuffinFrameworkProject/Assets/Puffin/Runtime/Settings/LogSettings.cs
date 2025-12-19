using System;
using System.Collections.Generic;
using UnityEngine;

namespace Puffin.Runtime.Settings
{
    /// <summary>
    /// 日志级别枚举
    /// </summary>
    public enum LogLevel
    {
        /// <summary>详细日志（最低级别）</summary>
        Verbose = 0,
        /// <summary>信息日志</summary>
        Info = 1,
        /// <summary>警告日志</summary>
        Warning = 2,
        /// <summary>错误日志</summary>
        Error = 3,
        /// <summary>禁用日志</summary>
        None = 4
    }

    /// <summary>
    /// 平台日志配置，用于针对不同平台设置不同的日志级别
    /// </summary>
    [Serializable]
    public class PlatformLogConfig
    {
        /// <summary>目标平台</summary>
        public RuntimePlatform platform;
        /// <summary>日志级别</summary>
        public LogLevel logLevel = LogLevel.Info;
        /// <summary>是否启用堆栈跟踪</summary>
        public bool enableStackTrace = true;
        /// <summary>是否启用颜色</summary>
        public bool enableColors = true;
    }

    /// <summary>
    /// 日志标签配置，用于自定义标签的颜色和启用状态
    /// </summary>
    [Serializable]
    public class LogTagConfig
    {
        /// <summary>标签名称</summary>
        public string tag;
        /// <summary>标签颜色</summary>
        public Color color = Color.white;
        /// <summary>是否启用</summary>
        public bool enabled = true;
    }

    /// <summary>
    /// 日志系统配置
    /// </summary>
    [SettingsPath("LogSettings")]
    [PuffinSetting("Log")]
    public class LogSettings : SettingsBase<LogSettings>
    {

        [Header("基础配置")]
        public LogLevel globalLogLevel = LogLevel.Info;
        public bool enableStackTrace = true;
        public bool enableColors = true;
        public int maxCollectionElements = 20;

        [Header("平台配置")]
        public List<PlatformLogConfig> platformConfigs = new();

        [Header("颜色配置")]
        public List<Color> infoColors = new()
        {
            new Color(1f, 0.88f, 0.73f),
            new Color(0.64f, 1f, 0.73f),
            new Color(0.63f, 1f, 0.83f),
            new Color(1f, 0.73f, 0.71f),
            new Color(1f, 0.73f, 0.91f),
            new Color(0.87f, 0.82f, 1f),
            new Color(1f, 0.76f, 0.55f),
            new Color(0.85f, 0.59f, 1f)
        };

        public Color warningColor = new(1f, 1f, 0.51f);
        public Color errorColor = new(1f, 0.18f, 0.11f);
        public Color tagColor = new(0.42f, 1f, 0.5f);

        [Header("自定义标签")]
        public List<LogTagConfig> customTags = new();


        /// <summary>
        /// 获取当前平台的日志配置
        /// </summary>
        public PlatformLogConfig GetCurrentPlatformConfig()
        {
            return platformConfigs.Find(c => c.platform == Application.platform);
        }

        /// <summary>
        /// 获取当前有效的日志级别（优先使用平台配置）
        /// </summary>
        public LogLevel GetEffectiveLogLevel()
        {
            var platformConfig = GetCurrentPlatformConfig();
            return platformConfig?.logLevel ?? globalLogLevel;
        }

        /// <summary>
        /// 获取指定标签的配置
        /// </summary>
        public LogTagConfig GetTagConfig(string tag)
        {
            return customTags.Find(t => t.tag == tag);
        }

        /// <summary>
        /// 获取指定索引的信息颜色
        /// </summary>
        public Color GetInfoColor(int index)
        {
            if (index >= 0 && index < infoColors.Count)
                return infoColors[index];
            return infoColors.Count > 0 ? infoColors[0] : Color.white;
        }

    }
}
