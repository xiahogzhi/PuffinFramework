using System;
using System.Linq;
using Puffin.Runtime.Settings;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor
{
    /// <summary>
    /// 编辑器启动时自动初始化所有设置
    /// </summary>
    [InitializeOnLoad]
    public static class SettingsInitializer
    {
        static SettingsInitializer()
        {
            // 同步执行，确保在其他编辑器初始化之前创建配置
            InitializeAllSettings();
            EditorApplication.delayCall += EnsureInitialized;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }
        
        
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // 从 Play 模式退出后重新初始化编辑器系统
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += EnsureInitialized;
        }

        public static bool IsInitialized { get; private set; }

        public static void EnsureInitialized()
        {
            if (!IsInitialized)
                InitializeAllSettings();
        }

        private static void InitializeAllSettings()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            var settingsBaseType = typeof(SettingsBase<>);
            var settingsTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t => !t.IsAbstract && !t.IsGenericType && IsSubclassOfGeneric(t, settingsBaseType))
                .ToList();

            foreach (var type in settingsTypes)
            {
                try
                {
                    var instanceProp = type.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
                    instanceProp?.GetValue(null);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SettingsInitializer] 初始化 {type.Name} 失败: {e.Message}");
                }
            }
        }

        private static bool IsSubclassOfGeneric(Type type, Type genericBase)
        {
            while (type != null && type != typeof(object))
            {
                var cur = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
                if (genericBase == cur)
                    return true;
                type = type.BaseType;
            }
            return false;
        }

      
    }
}
