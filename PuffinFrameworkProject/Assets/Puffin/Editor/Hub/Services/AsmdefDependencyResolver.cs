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
    /// 程序集依赖解析器
    /// </summary>
    [InitializeOnLoad]
    public static class AsmdefDependencyResolver
    {
        private static bool _isResolving;

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
            manifest.ParseReferences(out var asmdefRefs, out var dllRefs);

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
            if (_isResolving) return;
            _isResolving = true;

            try
            {
                // 暂时禁用清除引用功能，避免循环添加/删除
                // CleanupMissingReferences();

                var modulesDir = ManifestService.GetModulesPath();
                if (!Directory.Exists(modulesDir)) return;

                foreach (var dir in Directory.GetDirectories(modulesDir))
                {
                    // 跳过 ~ 目录
                    if (dir.Contains("~")) continue;

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

            }
            finally
            {
                _isResolving = false;
            }
        }

        private static string FindAsmdef(string dir)
        {
            if (!Directory.Exists(dir) || dir.Contains("~")) return null;
            var files = Directory.GetFiles(dir, "*.asmdef", SearchOption.TopDirectoryOnly);
            return files.FirstOrDefault(f => !f.Contains("~"));
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

            var newRefs = baseRefs.Concat(depRefs).Concat(preserved).Distinct().ToList();

            // 检查引用是否有变化
            var refsChanged = !data.references.OrderBy(x => x).SequenceEqual(newRefs.OrderBy(x => x));

            if (refsChanged)
                data.references = newRefs;

            var dllChanged = false;
            if (dllRefs?.Count > 0)
            {
                foreach (var dll in dllRefs.Where(d => !data.precompiledReferences.Contains(d)))
                {
                    data.precompiledReferences.Add(dll);
                    dllChanged = true;
                }
                if (dllChanged)
                    data.overrideReferences = true;
            }

            // 只有真正有变化时才写入
            if (refsChanged || dllChanged)
                File.WriteAllText(asmdefPath, JsonUtility.ToJson(data, true));
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
                // 跳过 ~ 目录
                if (moduleDir.Contains("~")) continue;

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
                AssetDatabase.Refresh();
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
                // 保留: GUID引用、存在的引用
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
