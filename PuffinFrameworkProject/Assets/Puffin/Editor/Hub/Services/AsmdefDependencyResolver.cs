#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Puffin.Editor.Hub.Data;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Hub.Services
{
    /// <summary>
    /// 程序集依赖解析器 - 用于管理模块间的 asmdef 引用关系
    /// 自动在编译后和 Unity 启动时处理环境依赖引用
    /// </summary>
    [InitializeOnLoad]
    public static class AsmdefDependencyResolver
    {
        static AsmdefDependencyResolver()
        {
            // 延迟执行，避免在编辑器初始化时出问题
            EditorApplication.delayCall += OnEditorReady;
            // 窗口获取焦点时刷新依赖引用
            EditorApplication.focusChanged += OnFocusChanged;
        }

        private static void OnEditorReady()
        {
            // 只执行一次
            EditorApplication.delayCall -= OnEditorReady;
            CleanupMissingReferences();
            ResolveAllModuleDependencies();
            ResolveAllEnvDependencies();
        }

        private static void OnFocusChanged(bool hasFocus)
        {
            if (hasFocus)
            {
                CleanupMissingReferences();
                ResolveAllModuleDependencies();
                ResolveAllEnvDependencies();
            }
        }

        // [MenuItem("Puffin Framework/解析程序集引用")]
        // public static void ResolveReferencesMenu()
        // {
        //     ResolveAllModuleDependencies();
        //     ResolveAllEnvDependencies();
        //     Debug.Log("[AsmdefResolver] 程序集引用解析完成");
        // }

        [Serializable]
        private class AsmdefData
        {
            public string name = "";
            public string rootNamespace = "";
            public List<string> references = new();
            public List<string> includePlatforms = new();
            public List<string> excludePlatforms = new();
            public bool allowUnsafeCode;
            public bool overrideReferences;
            public List<string> precompiledReferences = new();
            public bool autoReferenced = true;
            public List<string> defineConstraints = new();
            public List<object> versionDefines = new();
            public bool noEngineReferences;
        }

        /// <summary>
        /// 更新模块的 asmdef 引用（基于依赖列表，保留现有配置）
        /// </summary>
        public static void UpdateModuleAsmdefReferences(string moduleId, string modulePath, List<ModuleDependency> dependencies)
        {
            dependencies ??= new List<ModuleDependency>();

            // 构建需要的依赖引用（支持两种命名格式）
            var runtimeDepRefs = new List<string>();
            var editorDepRefs = new List<string>();
            foreach (var dep in dependencies)
            {
                if (!dep.optional)
                {
                    // 获取依赖模块的实际程序集名称
                    var depRuntimeName = GetModuleRuntimeAssemblyName(dep.moduleId);
                    var depEditorName = GetModuleEditorAssemblyName(dep.moduleId);

                    if (!string.IsNullOrEmpty(depRuntimeName))
                    {
                        runtimeDepRefs.Add(depRuntimeName);
                        editorDepRefs.Add(depRuntimeName);
                    }
                    if (!string.IsNullOrEmpty(depEditorName))
                        editorDepRefs.Add(depEditorName);
                }
            }

            // 查找 Runtime asmdef（支持多种命名格式）
            var runtimeAsmdefPath = FindAsmdefInDir(Path.Combine(modulePath, "Runtime"));
            if (!string.IsNullOrEmpty(runtimeAsmdefPath))
            {
                var baseRefs = new[] { "PuffinFramework.Runtime" };
                UpdateAsmdefReferences(runtimeAsmdefPath, baseRefs, runtimeDepRefs);
            }

            // 查找 Editor asmdef
            var editorAsmdefPath = FindAsmdefInDir(Path.Combine(modulePath, "Editor"));
            if (!string.IsNullOrEmpty(editorAsmdefPath))
            {
                // 获取当前模块的 Runtime 程序集名称
                var selfRuntimeName = !string.IsNullOrEmpty(runtimeAsmdefPath)
                    ? Path.GetFileNameWithoutExtension(runtimeAsmdefPath)
                    : $"{moduleId}.Runtime";
                var baseRefs = new[] { "PuffinFramework.Runtime", selfRuntimeName, "PuffinFramework.Editor" };
                UpdateAsmdefReferences(editorAsmdefPath, baseRefs, editorDepRefs);
            }
        }

        /// <summary>
        /// 在目录中查找 asmdef 文件
        /// </summary>
        private static string FindAsmdefInDir(string dir)
        {
            if (!Directory.Exists(dir)) return null;
            var files = Directory.GetFiles(dir, "*.asmdef", SearchOption.TopDirectoryOnly);
            return files.Length > 0 ? files[0] : null;
        }

        /// <summary>
        /// 获取模块的 Runtime 程序集名称
        /// </summary>
        private static string GetModuleRuntimeAssemblyName(string moduleId)
        {
            var modulePath = Path.Combine(Application.dataPath, $"Puffin/Modules/{moduleId}");
            var runtimeDir = Path.Combine(modulePath, "Runtime");
            var asmdefPath = FindAsmdefInDir(runtimeDir);
            if (string.IsNullOrEmpty(asmdefPath)) return null;
            return Path.GetFileNameWithoutExtension(asmdefPath);
        }

        /// <summary>
        /// 获取模块的 Editor 程序集名称
        /// </summary>
        private static string GetModuleEditorAssemblyName(string moduleId)
        {
            var modulePath = Path.Combine(Application.dataPath, $"Puffin/Modules/{moduleId}");
            var editorDir = Path.Combine(modulePath, "Editor");
            var asmdefPath = FindAsmdefInDir(editorDir);
            if (string.IsNullOrEmpty(asmdefPath)) return null;
            return Path.GetFileNameWithoutExtension(asmdefPath);
        }

        /// <summary>
        /// 更新 asmdef 引用，保留现有配置和非 Puffin 模块引用
        /// </summary>
        private static void UpdateAsmdefReferences(string asmdefPath, string[] baseRefs, List<string> depRefs)
        {
            var json = File.ReadAllText(asmdefPath);
            var data = JsonUtility.FromJson<AsmdefData>(json);

            // 保留非 PuffinFramework 模块的引用（用户手动添加的）
            var preservedRefs = data.references
                .Where(r => !r.StartsWith("PuffinFramework.") || r == "PuffinFramework.Runtime" || r == "PuffinFramework.Editor")
                .Where(r => !r.Contains(".Runtime") && !r.Contains(".Editor") || r == "PuffinFramework.Runtime" || r == "PuffinFramework.Editor")
                .ToList();

            // 也保留 GUID 引用和其他非模块引用
            preservedRefs.AddRange(data.references.Where(r => r.StartsWith("GUID:") || !r.StartsWith("PuffinFramework.")));
            preservedRefs = preservedRefs.Distinct().ToList();

            // 构建新的引用列表
            var newRefs = new List<string>();
            newRefs.AddRange(baseRefs);
            newRefs.AddRange(depRefs);
            newRefs.AddRange(preservedRefs.Where(r => !baseRefs.Contains(r) && !depRefs.Contains(r)));

            data.references = newRefs.Distinct().ToList();
            File.WriteAllText(asmdefPath, JsonUtility.ToJson(data, true));
        }

        /// <summary>
        /// 模块安装后更新程序集引用
        /// </summary>
        public static void OnModuleInstalled(string moduleId, List<string> dependencies)
        {
            var modulePath = Path.Combine(Application.dataPath, $"Puffin/Modules/{moduleId}");
            if (!Directory.Exists(modulePath)) return;

            var deps = dependencies?.Select(d => new ModuleDependency(d)).ToList() ?? new List<ModuleDependency>();
            UpdateModuleAsmdefReferences(moduleId, modulePath, deps);
        }

        /// <summary>
        /// 解析所有已安装模块的程序集依赖
        /// </summary>
        public static void ResolveAllModuleDependencies()
        {
            var modulesDir = Path.Combine(Application.dataPath, "Puffin/Modules");
            if (!Directory.Exists(modulesDir)) return;

            foreach (var moduleDir in Directory.GetDirectories(modulesDir))
            {
                var moduleId = Path.GetFileName(moduleDir);
                var manifestPath = Path.Combine(moduleDir, "module.json");

                if (!File.Exists(manifestPath)) continue;

                try
                {
                    var json = File.ReadAllText(manifestPath);
                    var manifest = JsonUtility.FromJson<HubModuleManifest>(json);
                    var deps = manifest.GetAllDependencies();
                    UpdateModuleAsmdefReferences(moduleId, moduleDir, deps);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AsmdefResolver] 解析模块 {moduleId} 失败: {e.Message}");
                }
            }

            AssetDatabase.Refresh();
            // Debug.Log("[AsmdefResolver] 程序集依赖解析完成");
        }

        /// <summary>
        /// 添加可选依赖的程序集引用（手动调用）
        /// </summary>
        public static void AddOptionalDependencyReference(string moduleId, string dependencyId)
        {
            var modulePath = Path.Combine(Application.dataPath, $"Puffin/Modules/{moduleId}");
            if (!Directory.Exists(modulePath)) return;

            var depRuntimeName = GetModuleRuntimeAssemblyName(dependencyId);
            var depEditorName = GetModuleEditorAssemblyName(dependencyId);

            // 更新 Runtime asmdef
            var runtimeAsmdefPath = FindAsmdefInDir(Path.Combine(modulePath, "Runtime"));
            if (!string.IsNullOrEmpty(runtimeAsmdefPath) && !string.IsNullOrEmpty(depRuntimeName))
                AddReference(runtimeAsmdefPath, depRuntimeName);

            // 更新 Editor asmdef
            var editorAsmdefPath = FindAsmdefInDir(Path.Combine(modulePath, "Editor"));
            if (!string.IsNullOrEmpty(editorAsmdefPath))
            {
                if (!string.IsNullOrEmpty(depRuntimeName))
                    AddReference(editorAsmdefPath, depRuntimeName);
                if (!string.IsNullOrEmpty(depEditorName))
                    AddReference(editorAsmdefPath, depEditorName);
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 移除可选依赖的程序集引用
        /// </summary>
        public static void RemoveOptionalDependencyReference(string moduleId, string dependencyId)
        {
            var modulePath = Path.Combine(Application.dataPath, $"Puffin/Modules/{moduleId}");
            if (!Directory.Exists(modulePath)) return;

            var depRuntimeName = GetModuleRuntimeAssemblyName(dependencyId);
            var depEditorName = GetModuleEditorAssemblyName(dependencyId);

            // 更新 Runtime asmdef
            var runtimeAsmdefPath = FindAsmdefInDir(Path.Combine(modulePath, "Runtime"));
            if (!string.IsNullOrEmpty(runtimeAsmdefPath) && !string.IsNullOrEmpty(depRuntimeName))
                RemoveReference(runtimeAsmdefPath, depRuntimeName);

            // 更新 Editor asmdef
            var editorAsmdefPath = FindAsmdefInDir(Path.Combine(modulePath, "Editor"));
            if (!string.IsNullOrEmpty(editorAsmdefPath))
            {
                if (!string.IsNullOrEmpty(depRuntimeName))
                    RemoveReference(editorAsmdefPath, depRuntimeName);
                if (!string.IsNullOrEmpty(depEditorName))
                    RemoveReference(editorAsmdefPath, depEditorName);
            }

            AssetDatabase.Refresh();
        }

        private static bool HasEditorAssembly(string moduleId)
        {
            var editorDir = Path.Combine(Application.dataPath, $"Puffin/Modules/{moduleId}/Editor");
            if (!Directory.Exists(editorDir)) return false;
            return Directory.GetFiles(editorDir, "*.asmdef", SearchOption.TopDirectoryOnly).Length > 0;
        }

        private static void AddReference(string asmdefPath, string referenceName)
        {
            if (!File.Exists(asmdefPath)) return;

            var json = File.ReadAllText(asmdefPath);
            var data = JsonUtility.FromJson<AsmdefData>(json);

            if (!data.references.Contains(referenceName))
            {
                data.references.Add(referenceName);
                File.WriteAllText(asmdefPath, JsonUtility.ToJson(data, true));
            }
        }

        private static void RemoveReference(string asmdefPath, string referenceName)
        {
            if (!File.Exists(asmdefPath)) return;

            var json = File.ReadAllText(asmdefPath);
            var data = JsonUtility.FromJson<AsmdefData>(json);

            if (data.references.Remove(referenceName))
                File.WriteAllText(asmdefPath, JsonUtility.ToJson(data, true));
        }

        /// <summary>
        /// 解析所有模块的环境依赖引用（自动调用）
        /// </summary>
        public static void ResolveAllEnvDependencies()
        {
            var modulesDir = Path.Combine(Application.dataPath, "Puffin/Modules");
            if (!Directory.Exists(modulesDir)) return;

            var depManager = new Environment.DependencyManager();
            var changed = false;

            foreach (var moduleDir in Directory.GetDirectories(modulesDir))
            {
                var moduleId = Path.GetFileName(moduleDir);
                var manifestPath = Path.Combine(moduleDir, "module.json");
                if (!File.Exists(manifestPath)) continue;

                try
                {
                    var json = File.ReadAllText(manifestPath);
                    var manifest = JsonUtility.FromJson<HubModuleManifest>(json);
                    if (manifest?.envDependencies == null) continue;

                    var runtimeDir = Path.Combine(moduleDir, "Runtime");
                    var editorDir = Path.Combine(moduleDir, "Editor");
                    var runtimeAsmdef = Directory.Exists(runtimeDir)
                        ? Directory.GetFiles(runtimeDir, "*.asmdef", SearchOption.TopDirectoryOnly).FirstOrDefault()
                        : null;
                    var editorAsmdef = Directory.Exists(editorDir)
                        ? Directory.GetFiles(editorDir, "*.asmdef", SearchOption.TopDirectoryOnly).FirstOrDefault()
                        : null;

                    foreach (var envDep in manifest.envDependencies)
                    {
                        // 检查依赖是否已安装
                        var dep = ConvertToDepDefinition(envDep);
                        var isInstalled = depManager.IsInstalled(dep);

                        if (isInstalled)
                        {
                            // 添加引用
                            if (envDep.dllReferences is {Length: > 0})
                            {
                                if (File.Exists(runtimeAsmdef) && AddDllReferences(runtimeAsmdef, envDep.dllReferences))
                                    changed = true;
                                if (File.Exists(editorAsmdef) && AddDllReferences(editorAsmdef, envDep.dllReferences))
                                    changed = true;
                            }
                            if (envDep.asmdefReferences is {Length: > 0})
                            {
                                foreach (var asmRef in envDep.asmdefReferences)
                                {
                                    if (File.Exists(runtimeAsmdef) && AddReferenceIfMissing(runtimeAsmdef, asmRef))
                                        changed = true;
                                    if (File.Exists(editorAsmdef) && AddReferenceIfMissing(editorAsmdef, asmRef))
                                        changed = true;
                                }
                            }
                        }
                        else
                        {
                            // 移除引用
                            if (envDep.dllReferences is {Length: > 0})
                            {
                                if (File.Exists(runtimeAsmdef) && RemoveDllReferences(runtimeAsmdef, envDep.dllReferences))
                                    changed = true;
                                if (File.Exists(editorAsmdef) && RemoveDllReferences(editorAsmdef, envDep.dllReferences))
                                    changed = true;
                            }
                            if (envDep.asmdefReferences is {Length: > 0})
                            {
                                foreach (var asmRef in envDep.asmdefReferences)
                                {
                                    if (File.Exists(runtimeAsmdef) && RemoveReferenceIfExists(runtimeAsmdef, asmRef))
                                        changed = true;
                                    if (File.Exists(editorAsmdef) && RemoveReferenceIfExists(editorAsmdef, asmRef))
                                        changed = true;
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AsmdefResolver] 处理模块 {moduleId} 环境依赖失败: {e.Message}");
                }
            }

            if (changed)
            {
                AssetDatabase.Refresh();
                // Debug.Log("[AsmdefResolver] 环境依赖引用已更新");
            }
        }

        private static Environment.DependencyDefinition ConvertToDepDefinition(EnvironmentDependency envDep)
        {
            return new Environment.DependencyDefinition
            {
                id = envDep.id,
                source = (Environment.DependencySource)envDep.source,
                type = (Environment.DependencyType)envDep.type,
                url = envDep.url,
                version = envDep.version,
                installDir = envDep.installDir,
                requiredFiles = envDep.requiredFiles,
                targetFrameworks = envDep.targetFrameworks
            };
        }

        private static bool AddReferenceIfMissing(string asmdefPath, string referenceName)
        {
            if (!File.Exists(asmdefPath)) return false;
            // 只添加确实存在的程序集（且不在 ~ 目录中）
            if (!AsmdefExists(referenceName)) return false;
            var json = File.ReadAllText(asmdefPath);
            var data = JsonUtility.FromJson<AsmdefData>(json);
            if (data.references.Contains(referenceName)) return false;
            data.references.Add(referenceName);
            File.WriteAllText(asmdefPath, JsonUtility.ToJson(data, true));
            return true;
        }

        private static bool RemoveReferenceIfExists(string asmdefPath, string referenceName)
        {
            if (!File.Exists(asmdefPath)) return false;
            var json = File.ReadAllText(asmdefPath);
            var data = JsonUtility.FromJson<AsmdefData>(json);
            if (!data.references.Remove(referenceName)) return false;
            File.WriteAllText(asmdefPath, JsonUtility.ToJson(data, true));
            return true;
        }

        private static bool AddDllReferences(string asmdefPath, string[] dllNames)
        {
            if (!File.Exists(asmdefPath)) return false;

            var json = File.ReadAllText(asmdefPath);
            var data = JsonUtility.FromJson<AsmdefData>(json);
            var changed = false;

            foreach (var dll in dllNames)
            {
                // 只添加确实存在的 DLL（且不在 ~ 目录中）
                if (!data.precompiledReferences.Contains(dll) && DllExists(dll))
                {
                    data.precompiledReferences.Add(dll);
                    changed = true;
                }
            }

            if (changed)
            {
                data.overrideReferences = true;
                File.WriteAllText(asmdefPath, JsonUtility.ToJson(data, true));
            }
            return changed;
        }

        private static bool RemoveDllReferences(string asmdefPath, string[] dllNames)
        {
            if (!File.Exists(asmdefPath)) return false;

            var json = File.ReadAllText(asmdefPath);
            var data = JsonUtility.FromJson<AsmdefData>(json);
            var changed = false;

            foreach (var dll in dllNames)
            {
                if (data.precompiledReferences.Remove(dll))
                    changed = true;
            }

            if (changed)
            {
                if (data.precompiledReferences.Count == 0)
                    data.overrideReferences = false;
                File.WriteAllText(asmdefPath, JsonUtility.ToJson(data, true));
            }
            return changed;
        }

        /// <summary>
        /// 清理所有模块中丢失的程序集和 DLL 引用
        /// </summary>
        public static void CleanupMissingReferences()
        {
            var modulesDir = Path.Combine(Application.dataPath, "Puffin/Modules");
            if (!Directory.Exists(modulesDir)) return;

            var changed = false;
            foreach (var moduleDir in Directory.GetDirectories(modulesDir))
            {
                var runtimeDir = Path.Combine(moduleDir, "Runtime");
                var editorDir = Path.Combine(moduleDir, "Editor");

                var runtimeAsmdef = Directory.Exists(runtimeDir)
                    ? Directory.GetFiles(runtimeDir, "*.asmdef", SearchOption.TopDirectoryOnly).FirstOrDefault()
                    : null;
                var editorAsmdef = Directory.Exists(editorDir)
                    ? Directory.GetFiles(editorDir, "*.asmdef", SearchOption.TopDirectoryOnly).FirstOrDefault()
                    : null;

                if (!string.IsNullOrEmpty(runtimeAsmdef) && CleanupAsmdefReferences(runtimeAsmdef))
                    changed = true;
                if (!string.IsNullOrEmpty(editorAsmdef) && CleanupAsmdefReferences(editorAsmdef))
                    changed = true;
            }

            if (changed)
            {
                AssetDatabase.Refresh();
                Debug.Log("[AsmdefResolver] 已清理丢失的引用");
            }
        }

        /// <summary>
        /// 清理单个 asmdef 中丢失的引用
        /// </summary>
        private static bool CleanupAsmdefReferences(string asmdefPath)
        {
            if (!File.Exists(asmdefPath)) return false;

            var json = File.ReadAllText(asmdefPath);
            var data = JsonUtility.FromJson<AsmdefData>(json);
            var changed = false;

            // 清理丢失的程序集引用
            var validRefs = new List<string>();
            foreach (var refName in data.references)
            {
                if (refName.StartsWith("GUID:") || AsmdefExists(refName))
                    validRefs.Add(refName);
                else
                    changed = true;
            }
            data.references = validRefs;

            // 清理丢失的 DLL 引用
            if (data.precompiledReferences.Count > 0)
            {
                var validDlls = new List<string>();
                foreach (var dllName in data.precompiledReferences)
                {
                    if (DllExists(dllName))
                        validDlls.Add(dllName);
                    else
                        changed = true;
                }
                data.precompiledReferences = validDlls;

                if (data.precompiledReferences.Count == 0)
                    data.overrideReferences = false;
            }

            if (changed)
                File.WriteAllText(asmdefPath, JsonUtility.ToJson(data, true));

            return changed;
        }

        /// <summary>
        /// 检查程序集定义是否存在
        /// </summary>
        private static bool AsmdefExists(string asmdefName)
        {
            var searchPaths = new[]
            {
                Application.dataPath,
                Path.Combine(Application.dataPath, "../Packages"),
                Path.Combine(Application.dataPath, "../Library/PackageCache")
            };

            foreach (var basePath in searchPaths)
            {
                if (!Directory.Exists(basePath)) continue;
                try
                {
                    var files = Directory.GetFiles(basePath, $"{asmdefName}.asmdef", SearchOption.AllDirectories);
                    // 跳过带 ~ 的目录（Unity 忽略的目录）
                    if (files.Any(f => !f.Contains("~"))) return true;
                }
                catch { }
            }
            return false;
        }

        /// <summary>
        /// 检查 DLL 是否存在
        /// </summary>
        private static bool DllExists(string dllName)
        {
            if (!dllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                dllName += ".dll";

            var searchPaths = new[]
            {
                Application.dataPath,
                Path.Combine(Application.dataPath, "../Packages"),
                Path.Combine(Application.dataPath, "../Library/PackageCache")
            };

            foreach (var basePath in searchPaths)
            {
                if (!Directory.Exists(basePath)) continue;
                try
                {
                    var files = Directory.GetFiles(basePath, dllName, SearchOption.AllDirectories);
                    // 跳过带 ~ 的目录（Unity 忽略的目录）
                    if (files.Any(f => !f.Contains("~"))) return true;
                }
                catch { }
            }
            return false;
        }

        private static void WriteAsmdef(string path, string name, string[] references, string[] includePlatforms)
        {
            var refsJson = string.Join(",\n        ", references.Select(r => $"\"{r}\""));
            var platformsJson = includePlatforms != null ? string.Join(",\n        ", includePlatforms.Select(p => $"\"{p}\"")) : "";

            var content = $@"{{
    ""name"": ""{name}"",
    ""rootNamespace"": """",
    ""references"": [
        {refsJson}
    ],
    ""includePlatforms"": [{(string.IsNullOrEmpty(platformsJson) ? "" : $"\n        {platformsJson}\n    ")}],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}}";
            File.WriteAllText(path, content);
        }
    }
}
#endif
