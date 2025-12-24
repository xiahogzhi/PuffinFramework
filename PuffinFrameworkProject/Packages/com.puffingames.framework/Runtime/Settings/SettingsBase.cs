using System;
using System.IO;
using Puffin.Runtime.Core;
using UnityEngine;

namespace Puffin.Runtime.Settings
{
    /// <summary>
    /// 设置路径特性，用于指定 Resources 下的路径
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SettingsPathAttribute : Attribute
    {
        public string Path { get; }
        public SettingsPathAttribute(string path) => Path = path;
    }

    /// <summary>
    /// 系统设置基类
    /// 继承此类并添加 [SettingsPath("YourSettingName")] 特性即可
    /// </summary>
    /// <typeparam name="T">设置类型</typeparam>
    public abstract class SettingsBase<T> : ScriptableObject where T : SettingsBase<T>
    {
        private static T _instance;

        /// <summary>
        /// 获取 ResourcePath
        /// </summary>
        public static string GetResourcePath()
        {
            var attr = (SettingsPathAttribute)Attribute.GetCustomAttribute(typeof(T), typeof(SettingsPathAttribute));
            return attr?.Path ?? typeof(T).Name;
        }

        /// <summary>
        /// 获取配置实例
        /// </summary>
        public static T Instance
        {
            get
            {
#if UNITY_EDITOR
                // 编辑器中始终从 AssetDatabase 加载，避免 Play 模式问题
                if (_instance == null)
                {
                     var path = GetResourcePath();
                    // 先尝试从模块 Resources 目录加载
                    // var moduleResourcesPath = GetModuleResourcesPath();
                    // var assetPath = $"Assets{moduleResourcesPath}/{path}.asset";
                    // var assetPath = $"Assets/Resources/{path}.asset";
                    _instance = Resources.Load<T>(path);
                    if (_instance == null)
                        _instance = CreateDefault(path);
                }
#else
                if (!PuffinFramework.IsSetup) throw new Exception("PuffinFramework is not setup");
                var c = CreateInstance<T>();
                c.LoadSetting();
                Destroy(c);
#endif
                return _instance;
            }
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        /// <returns></returns>
        protected virtual void LoadSetting()
        {
            if (_instance == null)
            {
                var path = GetResourcePath();
                _instance = PuffinFramework.ResourcesLoader.Load<T>(path);
            }
        }

        /// <summary>
        /// 清除缓存实例（用于域重载后重新加载）
        /// </summary>
        public static void ClearInstance()
        {
            _instance = null;
        }

#if UNITY_EDITOR
        // /// <summary>
        // /// 获取配置类所在模块的 Resources 目录路径
        // /// </summary>
        // private static string GetModuleResourcesPath()
        // {
        //     // 通过 MonoScript 查找配置类的脚本文件位置
        //     var scripts = UnityEditor.AssetDatabase.FindAssets($"t:MonoScript {typeof(T).Name}");
        //     foreach (var guid in scripts)
        //     {
        //         var scriptPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
        //         var script = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(scriptPath);
        //         if (script != null && script.GetClass() == typeof(T))
        //         {
        //             // 找到脚本所在目录，向上查找模块根目录（包含 Runtime 文件夹的目录）
        //             var dir = Path.GetDirectoryName(scriptPath)?.Replace("\\", "/");
        //             while (!string.IsNullOrEmpty(dir) && dir.StartsWith("Assets"))
        //             {
        //                 var dirName = Path.GetFileName(dir);
        //                 if (dirName == "Runtime" || dirName == "Editor")
        //                 {
        //                     // 模块根目录是 Runtime/Editor 的父目录
        //                     var moduleRoot = Path.GetDirectoryName(dir)?.Replace("\\", "/");
        //                     if (!string.IsNullOrEmpty(moduleRoot))
        //                         return $"{moduleRoot}/Resources";
        //                 }
        //
        //                 dir = Path.GetDirectoryName(dir)?.Replace("\\", "/");
        //             }
        //         }
        //     }
        //
        //     // 默认回退到框架 Resources 目录
        //     return "Assets/Puffin/Resources";
        // }

        private static T CreateDefault(string resourcePath)
        {
            var settings = CreateInstance<T>();

            var resourcesPath = "Assets/Resources";//GetModuleResourcesPath();
            if (!UnityEditor.AssetDatabase.IsValidFolder(resourcesPath))
            {
                var parent = Path.GetDirectoryName(resourcesPath)?.Replace("\\", "/");
                if (!string.IsNullOrEmpty(parent) && UnityEditor.AssetDatabase.IsValidFolder(parent))
                    UnityEditor.AssetDatabase.CreateFolder(parent, "Resources");
            }

            var assetPath = $"{resourcesPath}/{resourcePath}.asset";
            UnityEditor.AssetDatabase.CreateAsset(settings, assetPath);
            UnityEditor.AssetDatabase.SaveAssets();

            Debug.Log($"[PuffinFramework] 已创建默认配置: {assetPath}");
            return settings;
        }

        /// <summary>
        /// 在编辑器中选中此设置
        /// </summary>
        public static void SelectInEditor()
        {
            UnityEditor.Selection.activeObject = Instance;
        }
#endif
    }
}