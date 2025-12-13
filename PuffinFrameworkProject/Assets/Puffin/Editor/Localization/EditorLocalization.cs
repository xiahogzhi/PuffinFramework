#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Puffin.Runtime.Settings;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Localization
{
    /// <summary>
    /// 编辑器本地化工具
    /// </summary>
    public static class EditorLocalization
    {
        private static Dictionary<string, Dictionary<string, string>> _texts;
        private static string _currentLanguage;
        private static bool _initialized;

        private const string LocalizationFolder = "Assets/Puffin/Editor/Localization/Languages";
        private const string DefaultLanguageFile = "editor_zh.json";

        /// <summary>
        /// 当前语言代码
        /// </summary>
        public static string CurrentLanguage
        {
            get
            {
                if (_currentLanguage == null)
                {
                    var settings = Puffinettings.Instance;
                    _currentLanguage = settings != null ? GetLanguageCode(settings.editorLanguage) : "zh";
                }
                return _currentLanguage;
            }
        }

        /// <summary>
        /// 获取本地化文本
        /// </summary>
        public static string L(string key)
        {
            EnsureInitialized();

            if (_texts.TryGetValue(key, out var translations))
            {
                if (translations.TryGetValue(CurrentLanguage, out var text))
                    return text;
                // 回退到中文
                if (translations.TryGetValue("zh", out text))
                    return text;
                // 回退到任意语言
                foreach (var t in translations.Values)
                    return t;
            }

            return key; // 未找到则返回 key
        }

        /// <summary>
        /// 获取本地化文本（带格式化参数）
        /// </summary>
        public static string L(string key, params object[] args)
        {
            return string.Format(L(key), args);
        }

        /// <summary>
        /// 重新加载语言配置
        /// </summary>
        public static void Reload()
        {
            _initialized = false;
            _currentLanguage = null;
            _texts = null;
            EnsureInitialized();
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            _texts = new Dictionary<string, Dictionary<string, string>>();
            LoadAllLanguageFiles();
        }

        private static void LoadAllLanguageFiles()
        {
            if (!Directory.Exists(LocalizationFolder))
            {
                Directory.CreateDirectory(LocalizationFolder);
                CreateDefaultLanguageFiles();
                AssetDatabase.Refresh();
            }

            var files = Directory.GetFiles(LocalizationFolder, "*.json");
            foreach (var file in files)
            {
                LoadLanguageFile(file);
            }
        }

        private static void LoadLanguageFile(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                // 从文件名提取语言代码，如 editor_zh.json -> zh
                var langCode = fileName.Contains("_") ? fileName.Split('_')[1] : fileName;

                var data = JsonUtility.FromJson<LocalizationData>(json);
                if (data?.entries == null) return;

                foreach (var entry in data.entries)
                {
                    if (!_texts.ContainsKey(entry.key))
                        _texts[entry.key] = new Dictionary<string, string>();

                    _texts[entry.key][langCode] = entry.value;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EditorLocalization] 加载语言文件失败: {filePath}\n{e}");
            }
        }

        private static void CreateDefaultLanguageFiles()
        {
            // 中文
            var zhData = new LocalizationData
            {
                entries = new List<LocalizationEntry>
                {
                    // 通用
                    new() { key = "common.search", value = "搜索" },
                    new() { key = "common.sort", value = "排序" },
                    new() { key = "common.save", value = "保存" },
                    new() { key = "common.refresh", value = "刷新" },
                    new() { key = "common.enable", value = "启用" },
                    new() { key = "common.disable", value = "禁用" },
                    new() { key = "common.enabled", value = "启用" },
                    new() { key = "common.disabled", value = "禁用" },
                    new() { key = "common.all", value = "全部" },
                    new() { key = "common.operation", value = "操作" },

                    // System Monitor
                    new() { key = "monitor.title", value = "System Monitor" },
                    new() { key = "monitor.show_disabled", value = "显示禁用" },
                    new() { key = "monitor.profiling", value = "性能统计" },
                    new() { key = "monitor.pause", value = "暂停" },
                    new() { key = "monitor.resume", value = "恢复" },
                    new() { key = "monitor.status", value = "状态" },
                    new() { key = "monitor.system_name", value = "系统名称" },
                    new() { key = "monitor.priority", value = "优先级" },
                    new() { key = "monitor.last_ms", value = "上次(ms)" },
                    new() { key = "monitor.avg_ms", value = "平均(ms)" },
                    new() { key = "monitor.system_count", value = "系统数" },
                    new() { key = "monitor.total_time", value = "总耗时" },
                    new() { key = "monitor.not_initialized", value = "Puffin Framework 未初始化" },
                    new() { key = "monitor.editor_mode", value = "编辑器模式 - 仅显示已注册系统，性能数据需在 Play 模式下查看" },

                    // System Registry
                    new() { key = "registry.title", value = "System Registry" },
                    new() { key = "registry.show_disabled_only", value = "仅禁用" },
                    new() { key = "registry.show_enabled_only", value = "仅启用" },
                    new() { key = "registry.rescan", value = "重新扫描" },
                    new() { key = "registry.enable_all", value = "全部启用" },
                    new() { key = "registry.disable_all", value = "全部禁用" },
                    new() { key = "registry.dependencies", value = "依赖" },
                    new() { key = "registry.total", value = "总计" },
                    new() { key = "registry.saved", value = "配置已保存" },
                    new() { key = "registry.interface_selection", value = "接口实现选择" },

                    // Sort modes
                    new() { key = "sort.priority", value = "优先级" },
                    new() { key = "sort.name", value = "名称" },
                    new() { key = "sort.update_time", value = "更新时间" },

                    // Settings
                    new() { key = "settings.editor_config", value = "编辑器配置" },
                    new() { key = "settings.language", value = "语言" },
                    new() { key = "settings.scan_config", value = "扫描配置" },
                    new() { key = "settings.scan_mode", value = "扫描模式" },
                    new() { key = "settings.scan_mode_tip", value = "扫描模式" },
                    new() { key = "settings.require_autoregister", value = "只扫描 [AutoRegister]" },
                    new() { key = "settings.require_autoregister_tip", value = "是否只扫描带 [AutoRegister] 特性的系统" },
                    new() { key = "settings.assembly_names", value = "程序集名称" },
                    new() { key = "settings.assembly_names_tip", value = "要扫描的程序集名称（ScanMode.Specified 时生效）" },
                    new() { key = "settings.exclude_prefixes", value = "排除程序集前缀" },
                    new() { key = "settings.exclude_prefixes_tip", value = "排除的程序集前缀" },
                    new() { key = "settings.runtime_config", value = "Runtime 配置" },
                    new() { key = "settings.enable_profiling", value = "启用性能统计" },
                    new() { key = "settings.enable_profiling_tip", value = "是否启用性能统计" },
                    new() { key = "settings.symbols", value = "条件符号" },
                    new() { key = "settings.symbols_tip", value = "预定义的条件符号" },
                    new() { key = "settings.init_config", value = "初始化配置" },
                    new() { key = "settings.auto_init", value = "自动初始化" },
                    new() { key = "settings.auto_init_tip", value = "是否自动初始化（运行时进入 Play 模式自动初始化）" },
                    new() { key = "settings.editor_support", value = "编辑器支持" },
                    new() { key = "settings.editor_support_tip", value = "是否在编辑器模式下初始化支持 IEditorSupport 的系统" },
                    new() { key = "settings.log_config", value = "日志配置" },
                    new() { key = "settings.system_info_level", value = "系统信息级别" },
                    new() { key = "settings.system_info_level_tip", value = "系统信息输出级别" },

                    // GameScript
                    new() { key = "gamescript.field_not_assigned", value = "字段 '{0}' 未赋值" },
                    new() { key = "gamescript.assign_refs", value = "引用赋值" },

                    // Log Settings
                    new() { key = "log.basic_config", value = "基础配置" },
                    new() { key = "log.global_level", value = "全局日志等级" },
                    new() { key = "log.global_level_tip", value = "全局日志等级" },
                    new() { key = "log.stack_trace", value = "启用堆栈追踪" },
                    new() { key = "log.stack_trace_tip", value = "是否启用堆栈追踪" },
                    new() { key = "log.enable_colors", value = "启用颜色" },
                    new() { key = "log.enable_colors_tip", value = "是否启用颜色" },
                    new() { key = "log.max_elements", value = "集合最大元素数" },
                    new() { key = "log.max_elements_tip", value = "集合输出最大元素数" },
                    new() { key = "log.platform_config", value = "平台配置" },
                    new() { key = "log.platform_specific", value = "平台特定配置" },
                    new() { key = "log.platform_specific_tip", value = "平台特定配置（优先级高于全局配置）" },
                    new() { key = "log.color_config", value = "颜色配置" },
                    new() { key = "log.info_colors", value = "Info 颜色列表" },
                    new() { key = "log.warning_color", value = "Warning 颜色" },
                    new() { key = "log.error_color", value = "Error 颜色" },
                    new() { key = "log.tag_color", value = "标签颜色" },
                    new() { key = "log.custom_tags", value = "自定义标签" }
                }
            };
            File.WriteAllText(Path.Combine(LocalizationFolder, "editor_zh.json"), JsonUtility.ToJson(zhData, true));

            // 英文
            var enData = new LocalizationData
            {
                entries = new List<LocalizationEntry>
                {
                    // Common
                    new() { key = "common.search", value = "Search" },
                    new() { key = "common.sort", value = "Sort" },
                    new() { key = "common.save", value = "Save" },
                    new() { key = "common.refresh", value = "Refresh" },
                    new() { key = "common.enable", value = "Enable" },
                    new() { key = "common.disable", value = "Disable" },
                    new() { key = "common.enabled", value = "Enabled" },
                    new() { key = "common.disabled", value = "Disabled" },
                    new() { key = "common.all", value = "All" },
                    new() { key = "common.operation", value = "Action" },

                    // System Monitor
                    new() { key = "monitor.title", value = "System Monitor" },
                    new() { key = "monitor.show_disabled", value = "Show Disabled" },
                    new() { key = "monitor.profiling", value = "Profiling" },
                    new() { key = "monitor.pause", value = "Pause" },
                    new() { key = "monitor.resume", value = "Resume" },
                    new() { key = "monitor.status", value = "Status" },
                    new() { key = "monitor.system_name", value = "System Name" },
                    new() { key = "monitor.priority", value = "Priority" },
                    new() { key = "monitor.last_ms", value = "Last(ms)" },
                    new() { key = "monitor.avg_ms", value = "Avg(ms)" },
                    new() { key = "monitor.system_count", value = "Systems" },
                    new() { key = "monitor.total_time", value = "Total" },
                    new() { key = "monitor.not_initialized", value = "Puffin Framework not initialized" },
                    new() { key = "monitor.editor_mode", value = "Editor Mode - Only showing registered systems, performance data available in Play mode" },

                    // System Registry
                    new() { key = "registry.title", value = "System Registry" },
                    new() { key = "registry.show_disabled_only", value = "Disabled" },
                    new() { key = "registry.show_enabled_only", value = "Enabled" },
                    new() { key = "registry.rescan", value = "Rescan" },
                    new() { key = "registry.enable_all", value = "Enable All" },
                    new() { key = "registry.disable_all", value = "Disable All" },
                    new() { key = "registry.dependencies", value = "Dependencies" },
                    new() { key = "registry.total", value = "Total" },
                    new() { key = "registry.saved", value = "Settings saved" },
                    new() { key = "registry.interface_selection", value = "Interface Selection" },

                    // Sort modes
                    new() { key = "sort.priority", value = "Priority" },
                    new() { key = "sort.name", value = "Name" },
                    new() { key = "sort.update_time", value = "Update Time" },

                    // Settings
                    new() { key = "settings.editor_config", value = "Editor Config" },
                    new() { key = "settings.language", value = "Language" },
                    new() { key = "settings.scan_config", value = "Scan Config" },
                    new() { key = "settings.scan_mode", value = "Scan Mode" },
                    new() { key = "settings.scan_mode_tip", value = "Scan mode" },
                    new() { key = "settings.require_autoregister", value = "Require AutoRegister" },
                    new() { key = "settings.require_autoregister_tip", value = "Only scan systems with [AutoRegister] attribute" },
                    new() { key = "settings.assembly_names", value = "Assembly Names" },
                    new() { key = "settings.assembly_names_tip", value = "Assembly names to scan (when ScanMode.Specified)" },
                    new() { key = "settings.exclude_prefixes", value = "Exclude Prefixes" },
                    new() { key = "settings.exclude_prefixes_tip", value = "Excluded assembly prefixes" },
                    new() { key = "settings.runtime_config", value = "Runtime Config" },
                    new() { key = "settings.enable_profiling", value = "Enable Profiling" },
                    new() { key = "settings.enable_profiling_tip", value = "Enable performance profiling" },
                    new() { key = "settings.symbols", value = "Symbols" },
                    new() { key = "settings.symbols_tip", value = "Predefined conditional symbols" },
                    new() { key = "settings.init_config", value = "Initialize Config" },
                    new() { key = "settings.auto_init", value = "Auto Initialize" },
                    new() { key = "settings.auto_init_tip", value = "Auto initialize on Play mode" },
                    new() { key = "settings.editor_support", value = "Editor Support" },
                    new() { key = "settings.editor_support_tip", value = "Initialize IEditorSupport systems in editor mode" },
                    new() { key = "settings.log_config", value = "Log Config" },
                    new() { key = "settings.system_info_level", value = "System Info Level" },
                    new() { key = "settings.system_info_level_tip", value = "System info output level" },

                    // GameScript
                    new() { key = "gamescript.field_not_assigned", value = "Field '{0}' not assigned" },
                    new() { key = "gamescript.assign_refs", value = "Assign References" },

                    // Log Settings
                    new() { key = "log.basic_config", value = "Basic Config" },
                    new() { key = "log.global_level", value = "Global Log Level" },
                    new() { key = "log.global_level_tip", value = "Global log level" },
                    new() { key = "log.stack_trace", value = "Enable Stack Trace" },
                    new() { key = "log.stack_trace_tip", value = "Enable stack trace in logs" },
                    new() { key = "log.enable_colors", value = "Enable Colors" },
                    new() { key = "log.enable_colors_tip", value = "Enable colored output" },
                    new() { key = "log.max_elements", value = "Max Collection Elements" },
                    new() { key = "log.max_elements_tip", value = "Max elements when logging collections" },
                    new() { key = "log.platform_config", value = "Platform Config" },
                    new() { key = "log.platform_specific", value = "Platform Specific Config" },
                    new() { key = "log.platform_specific_tip", value = "Platform specific config (overrides global)" },
                    new() { key = "log.color_config", value = "Color Config" },
                    new() { key = "log.info_colors", value = "Info Colors" },
                    new() { key = "log.warning_color", value = "Warning Color" },
                    new() { key = "log.error_color", value = "Error Color" },
                    new() { key = "log.tag_color", value = "Tag Color" },
                    new() { key = "log.custom_tags", value = "Custom Tags" }
                }
            };
            File.WriteAllText(Path.Combine(LocalizationFolder, "editor_en.json"), JsonUtility.ToJson(enData, true));
        }

        private static string GetLanguageCode(EditorLanguage language)
        {
            return language switch
            {
                EditorLanguage.English => "en",
                _ => "zh"
            };
        }

        [Serializable]
        private class LocalizationData
        {
            public List<LocalizationEntry> entries;
        }

        [Serializable]
        private class LocalizationEntry
        {
            public string key;
            public string value;
        }
    }
}
#endif
