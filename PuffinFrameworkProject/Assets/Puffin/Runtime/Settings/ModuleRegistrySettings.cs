using System;
using System.Collections.Generic;
using UnityEngine;

namespace Puffin.Runtime.Settings
{
    /// <summary>
    /// 模块注册条目
    /// </summary>
    [Serializable]
    public class ModuleEntry
    {
        public string moduleId;
        public bool enabled = true;
        public List<string> dependencies = new();
    }

    /// <summary>
    /// 模块注册配置 - 管理模块的启用/禁用状态
    /// </summary>
    [SettingsPath("ModuleRegistrySettings")]
    [CreateAssetMenu(fileName = "ModuleRegistrySettings", menuName = "PuffinFramework/Module Registry Settings")]
    public class ModuleRegistrySettings : SettingsBase<ModuleRegistrySettings>
    {
        [Tooltip("模块列表")]
        public List<ModuleEntry> modules = new();

        // 缓存：考虑依赖后的实际启用状态
        private Dictionary<string, bool> _effectiveStateCache;
        private HashSet<string> _disabledAssemblyPrefixes;

        /// <summary>
        /// 检查模块是否启用（考虑依赖关系）
        /// </summary>
        public bool IsModuleEnabled(string moduleId)
        {
            if (_effectiveStateCache == null)
                RebuildCache();

            return _effectiveStateCache.TryGetValue(moduleId, out var enabled) && enabled;
        }

        /// <summary>
        /// 检查程序集是否被禁用（属于禁用的模块）
        /// </summary>
        public bool IsAssemblyDisabled(string assemblyName)
        {
            if (_disabledAssemblyPrefixes == null)
                RebuildCache();

            foreach (var prefix in _disabledAssemblyPrefixes)
            {
                if (assemblyName.Contains(prefix))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 获取禁用模块的程序集前缀集合
        /// </summary>
        public HashSet<string> GetDisabledAssemblyPrefixes()
        {
            if (_disabledAssemblyPrefixes == null)
                RebuildCache();

            return _disabledAssemblyPrefixes;
        }

        /// <summary>
        /// 重建缓存（处理依赖传递）
        /// </summary>
        public void RebuildCache()
        {
            _effectiveStateCache = new Dictionary<string, bool>();
            _disabledAssemblyPrefixes = new HashSet<string>();

            // 构建模块字典和依赖图
            var moduleDict = new Dictionary<string, ModuleEntry>();
            foreach (var entry in modules)
            {
                if (!string.IsNullOrEmpty(entry.moduleId))
                    moduleDict[entry.moduleId] = entry;
            }

            // 初始化：直接禁用的模块
            foreach (var entry in modules)
            {
                if (!string.IsNullOrEmpty(entry.moduleId))
                    _effectiveStateCache[entry.moduleId] = entry.enabled;
            }

            // 传递禁用：如果依赖的模块被禁用，则此模块也禁用
            bool changed;
            do
            {
                changed = false;
                foreach (var entry in modules)
                {
                    if (string.IsNullOrEmpty(entry.moduleId))
                        continue;

                    // 已经禁用的跳过
                    if (!_effectiveStateCache[entry.moduleId])
                        continue;

                    // 检查依赖
                    foreach (var dep in entry.dependencies)
                    {
                        // 依赖的模块不存在或被禁用
                        if (!_effectiveStateCache.TryGetValue(dep, out var depEnabled) || !depEnabled)
                        {
                            _effectiveStateCache[entry.moduleId] = false;
                            changed = true;
                            break;
                        }
                    }
                }
            } while (changed);

            // 收集禁用模块的程序集前缀
            foreach (var kvp in _effectiveStateCache)
            {
                if (!kvp.Value)
                    _disabledAssemblyPrefixes.Add(kvp.Key);
            }
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void ClearCache()
        {
            _effectiveStateCache = null;
            _disabledAssemblyPrefixes = null;
        }

        private void OnValidate()
        {
            ClearCache();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => OnSettingsChanged?.Invoke();
#endif
        }

#if UNITY_EDITOR
        public static event System.Action OnSettingsChanged;

        public static void NotifySettingsChanged()
        {
            OnSettingsChanged?.Invoke();
        }
#endif
    }
}
