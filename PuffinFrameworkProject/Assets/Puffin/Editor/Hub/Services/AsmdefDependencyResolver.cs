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
        }

        private static void OnEditorReady()
        {
            // 只执行一次
            EditorApplication.delayCall -= OnEditorReady;
            ResolveAllEnvDependencies();
        }

        [MenuItem("Puffin Framework/解析程序集引用")]
        public static void ResolveReferencesMenu()
        {
            ResolveAllModuleDependencies();
            ResolveAllEnvDependencies();
            Debug.Log("[AsmdefResolver] 程序集引用解析完成");
        }

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

            // 构建需要的依赖引用
            var runtimeDepRefs = new List<string>();
            var editorDepRefs = new List<string>();
            foreach (var dep in dependencies)
            {
                if (!dep.optional)
                {
                    runtimeDepRefs.Add($"PuffinFramework.{dep.moduleId}.Runtime");
                    editorDepRefs.Add($"PuffinFramework.{dep.moduleId}.Runtime");
                    if (HasEditorAssembly(dep.moduleId))
                        editorDepRefs.Add($"PuffinFramework.{dep.moduleId}.Editor");
                }
            }

            // 更新 Runtime asmdef（保留现有配置）
            var runtimeAsmdefPath = Path.Combine(modulePath, "Runtime", $"PuffinFramework.{moduleId}.Runtime.asmdef");
            if (File.Exists(runtimeAsmdefPath))
            {
                var baseRefs = new[] { "PuffinFramework.Runtime" };
                UpdateAsmdefReferences(runtimeAsmdefPath, baseRefs, runtimeDepRefs);
            }

            // 更新 Editor asmdef（保留现有配置）
            var editorAsmdefPath = Path.Combine(modulePath, "Editor", $"PuffinFramework.{moduleId}.Editor.asmdef");
            if (File.Exists(editorAsmdefPath))
            {
                var baseRefs = new[] { "PuffinFramework.Runtime", $"PuffinFramework.{moduleId}.Runtime", "PuffinFramework.Editor" };
                UpdateAsmdefReferences(editorAsmdefPath, baseRefs, editorDepRefs);
            }
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
            Debug.Log("[AsmdefResolver] 程序集依赖解析完成");
        }

        /// <summary>
        /// 添加可选依赖的程序集引用（手动调用）
        /// </summary>
        public static void AddOptionalDependencyReference(string moduleId, string dependencyId)
        {
            var modulePath = Path.Combine(Application.dataPath, $"Puffin/Modules/{moduleId}");
            if (!Directory.Exists(modulePath)) return;

            // 更新 Runtime asmdef
            var runtimeAsmdefPath = Path.Combine(modulePath, "Runtime", $"PuffinFramework.{moduleId}.Runtime.asmdef");
            if (File.Exists(runtimeAsmdefPath))
                AddReference(runtimeAsmdefPath, $"PuffinFramework.{dependencyId}.Runtime");

            // 更新 Editor asmdef
            var editorAsmdefPath = Path.Combine(modulePath, "Editor", $"PuffinFramework.{moduleId}.Editor.asmdef");
            if (File.Exists(editorAsmdefPath))
            {
                AddReference(editorAsmdefPath, $"PuffinFramework.{dependencyId}.Runtime");
                if (HasEditorAssembly(dependencyId))
                    AddReference(editorAsmdefPath, $"PuffinFramework.{dependencyId}.Editor");
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

            // 更新 Runtime asmdef
            var runtimeAsmdefPath = Path.Combine(modulePath, "Runtime", $"PuffinFramework.{moduleId}.Runtime.asmdef");
            if (File.Exists(runtimeAsmdefPath))
                RemoveReference(runtimeAsmdefPath, $"PuffinFramework.{dependencyId}.Runtime");

            // 更新 Editor asmdef
            var editorAsmdefPath = Path.Combine(modulePath, "Editor", $"PuffinFramework.{moduleId}.Editor.asmdef");
            if (File.Exists(editorAsmdefPath))
            {
                RemoveReference(editorAsmdefPath, $"PuffinFramework.{dependencyId}.Runtime");
                RemoveReference(editorAsmdefPath, $"PuffinFramework.{dependencyId}.Editor");
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
                Debug.Log("[AsmdefResolver] 环境依赖引用已更新");
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
                if (!data.precompiledReferences.Contains(dll))
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
