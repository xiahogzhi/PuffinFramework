using System;
using System.Collections.Generic;
using UnityEngine;

namespace Puffin.Runtime.Settings
{
    public enum LogLevel
    {
        Verbose = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        None = 4
    }

    [Serializable]
    public class PlatformLogConfig
    {
        public RuntimePlatform platform;
        public LogLevel logLevel = LogLevel.Info;
        public bool enableStackTrace = true;
        public bool enableColors = true;
    }

    [Serializable]
    public class LogTagConfig
    {
        public string tag;
        public Color color = Color.white;
        public bool enabled = true;
    }

    [SettingsPath("LogSettings")]
    // [CreateAssetMenu(fileName = "LogSettings", menuName = "PuffinFramework/Log Settings")]
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


        public PlatformLogConfig GetCurrentPlatformConfig()
        {
            return platformConfigs.Find(c => c.platform == Application.platform);
        }

        public LogLevel GetEffectiveLogLevel()
        {
            var platformConfig = GetCurrentPlatformConfig();
            return platformConfig?.logLevel ?? globalLogLevel;
        }

        public LogTagConfig GetTagConfig(string tag)
        {
            return customTags.Find(t => t.tag == tag);
        }

        public Color GetInfoColor(int index)
        {
            if (index >= 0 && index < infoColors.Count)
                return infoColors[index];
            return infoColors.Count > 0 ? infoColors[0] : Color.white;
        }

    }
}
