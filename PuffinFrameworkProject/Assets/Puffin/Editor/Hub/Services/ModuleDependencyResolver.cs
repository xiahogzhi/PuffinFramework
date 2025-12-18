#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Puffin.Editor.Hub;
using UnityEditor;

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
        private static readonly HashSet<string> _pendingModules = new();

        static ModuleDependencyResolver()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            EditorApplication.delayCall -= OnEditorReady;
            SetupFileWatcher();
        }

        private static void SetupFileWatcher()
        {
            var modulesDir = ManifestService.GetModulesPath();
            if (!Directory.Exists(modulesDir))
                Directory.CreateDirectory(modulesDir);

            _watcher = new FileSystemWatcher(modulesDir)
            {
                Filter = HubConstants.ManifestFileName,
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

            AsmdefDependencyResolver.ResolveAll();
        }
    }
}
#endif
