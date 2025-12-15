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
    /// 模块依赖解析器 - 检查模块依赖并禁用缺少依赖的模块
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

            // 启动时检查所有模块依赖
            CheckAllModuleDependencies();

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

            // 检查所有模块依赖并更新程序集引用
            CheckAllModuleDependencies();
            AsmdefDependencyResolver.ResolveAllModuleDependencies();
        }

        /// <summary>
        /// 检查所有已安装模块的依赖，禁用缺少依赖的模块
        /// </summary>
        [MenuItem("Puffin Framework/检查模块依赖")]
        public static void CheckAllModuleDependencies()
        {
            if (_isProcessing) return;
            _isProcessing = true;

            try
            {
                var modulesDir = Path.Combine(Application.dataPath, "Puffin/Modules");
                if (!Directory.Exists(modulesDir)) return;

                var installer = new ModuleInstaller(new RegistryService(), new ModuleResolver(new RegistryService()));
                var modulesToDisable = new List<string>();

                // 检查每个已安装模块的依赖
                foreach (var moduleDir in Directory.GetDirectories(modulesDir))
                {
                    var moduleId = Path.GetFileName(moduleDir);
                    var missing = installer.GetMissingDependencies(moduleId);

                    if (missing.Count > 0)
                    {
                        Debug.LogWarning($"[ModuleDependencyResolver] 模块 {moduleId} 缺少依赖: {string.Join(", ", missing)}，将被禁用");
                        modulesToDisable.Add(moduleId);
                    }
                }

                // 禁用缺少依赖的模块
                foreach (var moduleId in modulesToDisable)
                {
                    installer.DisableModule(moduleId);
                }

                // 尝试启用可以启用的禁用模块
                TryEnableDisabledModules(installer);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ModuleDependencyResolver] 检查模块依赖失败: {e}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        /// 尝试启用所有可以启用的禁用模块（跳过手动禁用的）
        /// </summary>
        private static void TryEnableDisabledModules(ModuleInstaller installer)
        {
            var disabledModules = InstalledModulesLock.Instance.GetDisabledModules();
            foreach (var module in disabledModules)
            {
                // 跳过手动禁用的模块
                if (module.isManuallyDisabled) continue;

                if (installer.CanEnableModule(module.moduleId))
                {
                    Debug.Log($"[ModuleDependencyResolver] 模块 {module.moduleId} 的依赖已满足，正在启用...");
                    installer.EnableModule(module.moduleId);
                }
            }
        }

        /// <summary>
        /// 当模块被卸载时，检查并禁用依赖它的模块
        /// </summary>
        public static void OnModuleUninstalled(string uninstalledModuleId)
        {
            var modulesDir = Path.Combine(Application.dataPath, "Puffin/Modules");
            if (!Directory.Exists(modulesDir)) return;

            var installer = new ModuleInstaller(new RegistryService(), new ModuleResolver(new RegistryService()));
            var modulesToDisable = new List<string>();

            foreach (var moduleDir in Directory.GetDirectories(modulesDir))
            {
                var moduleId = Path.GetFileName(moduleDir);
                var manifestPath = Path.Combine(moduleDir, "module.json");
                if (!File.Exists(manifestPath)) continue;

                try
                {
                    var json = File.ReadAllText(manifestPath);
                    var manifest = JsonUtility.FromJson<HubModuleManifest>(json);
                    var deps = manifest?.GetAllDependencies();
                    if (deps == null) continue;

                    foreach (var dep in deps)
                    {
                        if (!dep.optional && dep.moduleId == uninstalledModuleId)
                        {
                            modulesToDisable.Add(moduleId);
                            break;
                        }
                    }
                }
                catch { }
            }

            foreach (var moduleId in modulesToDisable)
            {
                Debug.Log($"[ModuleDependencyResolver] 模块 {moduleId} 依赖的 {uninstalledModuleId} 已卸载，正在禁用...");
                installer.DisableModule(moduleId);
            }
        }
    }
}
#endif
