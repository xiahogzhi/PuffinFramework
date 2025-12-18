#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Puffin.Editor.Hub;
using Puffin.Editor.Hub.Data;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Hub.Services
{
    /// <summary>
    /// 程序集依赖解析器
    /// </summary>
    [InitializeOnLoad]
    public static class AsmdefDependencyResolver
    {
        static AsmdefDependencyResolver()
        {
            EditorApplication.delayCall += () => ResolveAll();
            EditorApplication.focusChanged += f => { if (f) ResolveAll(); };
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

        public static void UpdateReferences(string moduleId, string modulePath, HubModuleManifest manifest)
        {
            manifest ??= new HubModuleManifest();

            var moduleRefs = manifest.GetAllModuleReferences();
            var asmdefRefs = manifest.GetAsmdefReferences();
            var dllRefs = manifest.GetDllReferences();

            var runtimeRefs = new List<string>();
            var editorRefs = new List<string>();

            foreach (var modRef in moduleRefs.Where(r => !r.optional))
            {
                if (modRef.includeRuntime)
                {
                    var name = GetAssemblyName(modRef.moduleId, "Runtime");
                    if (name != null) { runtimeRefs.Add(name); editorRefs.Add(name); }
                }
                if (modRef.includeEditor)
                {
                    var name = GetAssemblyName(modRef.moduleId, "Editor");
                    if (name != null) editorRefs.Add(name);
                }
            }

            // 处理 asmdef 引用（#前缀为可选，不存在则跳过）
            foreach (var asmRef in asmdefRefs)
            {
                var isOptional = asmRef.StartsWith("#");
                var actualName = isOptional ? asmRef.Substring(1) : asmRef;
                if (isOptional && !IsAsmdefExists(actualName)) continue;
                runtimeRefs.Add(actualName);
                editorRefs.Add(actualName);
            }

            // 处理 dll 引用（#前缀为可选，不存在则跳过）
            var filteredDllRefs = new List<string>();
            foreach (var dllRef in dllRefs)
            {
                var isOptional = dllRef.StartsWith("#");
                var actualName = isOptional ? dllRef.Substring(1) : dllRef;
                if (isOptional && !IsDllExists(actualName)) continue;
                filteredDllRefs.Add(actualName);
            }

            var runtimeAsmdef = FindAsmdef(Path.Combine(modulePath, "Runtime"));
            if (runtimeAsmdef != null)
                WriteReferences(runtimeAsmdef, new[] { "PuffinFramework.Runtime" }, runtimeRefs, filteredDllRefs);

            var editorAsmdef = FindAsmdef(Path.Combine(modulePath, "Editor"));
            if (editorAsmdef != null)
            {
                var selfRuntime = runtimeAsmdef != null ? Path.GetFileNameWithoutExtension(runtimeAsmdef) : $"{moduleId}.Runtime";
                WriteReferences(editorAsmdef, new[] { "PuffinFramework.Runtime", selfRuntime, "PuffinFramework.Editor" }, editorRefs, filteredDllRefs);
            }
        }

        public static void ResolveAll()
        {
            CleanupMissingReferences();

            var modulesDir = ManifestService.GetModulesPath();
            if (!Directory.Exists(modulesDir)) return;

            foreach (var dir in Directory.GetDirectories(modulesDir))
            {
                var manifestPath = ManifestService.GetManifestPathFromDir(dir);
                if (!File.Exists(manifestPath)) continue;

                try
                {
                    var manifest = ManifestService.Load(manifestPath);
                    UpdateReferences(Path.GetFileName(dir), dir, manifest);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AsmdefResolver] {Path.GetFileName(dir)}: {e.Message}");
                }
            }

            ResolveAllEnvDependencies();
            AssetDatabase.Refresh();
        }

        private static string FindAsmdef(string dir)
        {
            if (!Directory.Exists(dir)) return null;
            var files = Directory.GetFiles(dir, "*.asmdef", SearchOption.TopDirectoryOnly);
            return files.Length > 0 ? files[0] : null;
        }

        private static string GetAssemblyName(string moduleId, string type)
        {
            var asmdef = FindAsmdef(Path.Combine(ManifestService.GetModulePath(moduleId), type));
            return asmdef != null ? Path.GetFileNameWithoutExtension(asmdef) : null;
        }

        private static bool IsAsmdefExists(string asmdefName)
        {
            var guids = AssetDatabase.FindAssets($"t:asmdef {asmdefName}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == asmdefName)
                    return true;
            }
            return false;
        }

        private static bool IsDllExists(string dllName)
        {
            var searchName = dllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(dllName)
                : dllName;
            var guids = AssetDatabase.FindAssets($"t:DefaultAsset {searchName}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileNameWithoutExtension(path).Equals(searchName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void WriteReferences(string asmdefPath, string[] baseRefs, List<string> depRefs, List<string> dllRefs)
        {
            var json = File.ReadAllText(asmdefPath);
            var data = JsonUtility.FromJson<AsmdefData>(json);

            // 保留非模块引用
            var preserved = data.references.Where(r => r.StartsWith("GUID:") ||
                (!r.Contains(".Runtime") && !r.Contains(".Editor") && !r.StartsWith("PuffinFramework."))).ToList();

            data.references = baseRefs.Concat(depRefs).Concat(preserved).Distinct().ToList();

            if (dllRefs?.Count > 0)
            {
                data.overrideReferences = true;
                foreach (var dll in dllRefs.Where(d => !data.precompiledReferences.Contains(d)))
                    data.precompiledReferences.Add(dll);
            }

            File.WriteAllText(asmdefPath, JsonUtility.ToJson(data, true));
        }

        /// <summary>
        /// 解析所有模块的环境依赖引用
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
                        var dep = ConvertToDepDefinition(envDep);
                        var isInstalled = depManager.IsInstalled(dep);

                        if (isInstalled)
                        {
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
                AssetDatabase.Refresh();
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

        private static bool CleanupAsmdefReferences(string asmdefPath)
        {
            if (!File.Exists(asmdefPath)) return false;

            var json = File.ReadAllText(asmdefPath);
            var data = JsonUtility.FromJson<AsmdefData>(json);
            var changed = false;

            var validRefs = new List<string>();
            foreach (var refName in data.references)
            {
                if (refName.StartsWith("GUID:") || AsmdefExists(refName))
                    validRefs.Add(refName);
                else
                    changed = true;
            }
            data.references = validRefs;

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
                    if (files.Any(f => !f.Contains("~"))) return true;
                }
                catch { }
            }
            return false;
        }

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
                    if (files.Any(f => !f.Contains("~"))) return true;
                }
                catch { }
            }
            return false;
        }
    }
}
#endif
