using System;
using System.Collections.Generic;
using UnityEngine;

namespace Puffin.Runtime.Settings
{
    /// <summary>
    /// 系统注册信息
    /// </summary>
    [Serializable]
    public class SystemRegistryEntry
    {
        public string typeName;
        public string displayName;
        public bool enabled = true;
        public int priority;
        public string alias;
        public List<string> dependencies = new();
    }

    /// <summary>
    /// 接口实现选择配置
    /// </summary>
    [Serializable]
    public class InterfaceImplementationEntry
    {
        public string interfaceTypeName;
        public string selectedImplementation;
        public bool enabled = true;
    }

    /// <summary>
    /// 系统注册配置 - 控制哪些系统可以被注册
    /// </summary>
    [SettingsPath("SystemRegistrySettings")]
    // [CreateAssetMenu(fileName = "SystemRegistrySettings", menuName = "PuffinFramework/System Registry Settings")]
    public class SystemRegistrySettings : SettingsBase<SystemRegistrySettings>
    {
        [Tooltip("系统注册列表")]
        public List<SystemRegistryEntry> systems = new();

        [Tooltip("接口实现选择")]
        public List<InterfaceImplementationEntry> interfaceSelections = new();

        private HashSet<string> _disabledCache;
        private Dictionary<string, string> _interfaceSelectionCache;

        /// <summary>
        /// 检查系统是否被禁用
        /// </summary>
        public bool IsSystemDisabled(Type type)
        {
            return IsSystemDisabled(type.FullName);
        }

        /// <summary>
        /// 检查系统是否被禁用
        /// </summary>
        public bool IsSystemDisabled(string typeName)
        {
            if (_disabledCache == null)
                RebuildCache();

            return _disabledCache.Contains(typeName);
        }

        /// <summary>
        /// 获取所有被禁用的系统类型名
        /// </summary>
        public HashSet<string> GetDisabledSystems()
        {
            if (_disabledCache == null)
                RebuildCache();

            return _disabledCache;
        }

        /// <summary>
        /// 重建缓存
        /// </summary>
        public void RebuildCache()
        {
            _disabledCache = new HashSet<string>();
            foreach (var entry in systems)
            {
                if (!entry.enabled)
                    _disabledCache.Add(entry.typeName);
            }

            _interfaceSelectionCache = new Dictionary<string, string>();
            foreach (var entry in interfaceSelections)
            {
                if (!string.IsNullOrEmpty(entry.interfaceTypeName) && !string.IsNullOrEmpty(entry.selectedImplementation))
                    _interfaceSelectionCache[entry.interfaceTypeName] = entry.selectedImplementation;
            }
        }

        /// <summary>
        /// 清除缓存（编辑器修改后调用）
        /// </summary>
        public void ClearCache()
        {
            _disabledCache = null;
            _interfaceSelectionCache = null;
        }

        /// <summary>
        /// 获取接口指定的实现类型名
        /// </summary>
        public string GetSelectedImplementation(string interfaceTypeName)
        {
            if (_interfaceSelectionCache == null)
                RebuildCache();

            return _interfaceSelectionCache.TryGetValue(interfaceTypeName, out var impl) ? impl : null;
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
