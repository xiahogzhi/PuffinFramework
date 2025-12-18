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
    }
}
#endif
