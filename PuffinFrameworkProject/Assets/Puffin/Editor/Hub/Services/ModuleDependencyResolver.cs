#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Puffin.Editor.Hub.Data;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Hub.Services
{
    /// <summary>
    /// 模块依赖解析器 - 检查模块依赖
    /// 在程序集依赖处理前执行
    /// </summary>
    [InitializeOnLoad]
    public static class ModuleDependencyResolver
    {
        private static FileSystemWatcher _watcher;
        private static bool _isProcessing;
        private static readonly HashSet<string> _pendingModules = new();

        static ModuleDependencyResolver()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            EditorApplication.delayCall -= OnEditorReady;

            // 设置文件监听
            SetupFileWatcher();
        }

        private static void SetupFileWatcher()
        {
            var modulesDir = Path.Combine(Application.dataPath, "Puffin/Modules");
            if (!Directory.Exists(modulesDir))
                Directory.CreateDirectory(modulesDir);

            _watcher = new FileSystemWatcher(modulesDir)
            {
                Filter = "module.json",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };

            _watcher.Changed += OnModuleJsonChanged;
            _watcher.Created += OnModuleJsonChanged;
            _watcher.EnableRaisingEvents = true;
        }

        private static void OnModuleJsonChanged(object sender, FileSystemEventArgs e)
        {
            var moduleDir = Path.GetDirectoryName(e.FullPath);
            var moduleId = Path.GetFileName(moduleDir);

            lock (_pendingModules)
            {
                _pendingModules.Add(moduleId);
            }

            // 延迟处理，避免频繁触发
            EditorApplication.delayCall -= ProcessPendingModules;
            EditorApplication.delayCall += ProcessPendingModules;
        }

        private static void ProcessPendingModules()
        {
            EditorApplication.delayCall -= ProcessPendingModules;

            HashSet<string> modules;
            lock (_pendingModules)
            {
                if (_pendingModules.Count == 0) return;
                modules = new HashSet<string>(_pendingModules);
                _pendingModules.Clear();
            }

            // 更新程序集引用
            AsmdefDependencyResolver.ResolveAllModuleDependencies();
        }
    }
}
#endif
