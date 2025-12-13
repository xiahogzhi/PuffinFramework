#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Environment
{
    public static class AsmdefHelper
    {
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
        /// 安装后处理：检测并创建 asmdef，添加引用到相关模块
        /// </summary>
        public static void OnInstalled(DependencyDefinition dep, string destDir)
        {
            var hasCs = Directory.GetFiles(destDir, "*.cs", SearchOption.AllDirectories).Length > 0;
            var hasDll = Directory.GetFiles(destDir, "*.dll", SearchOption.AllDirectories).Length > 0;
            var existingAsmdef = Directory.GetFiles(destDir, "*.asmdef", SearchOption.TopDirectoryOnly).FirstOrDefault();

            string asmdefName = null;

            // 有代码但没有 asmdef，创建一个
            if (hasCs && existingAsmdef == null)
            {
                asmdefName = dep.asmdefName ?? dep.id.Replace(".", "_");
                CreateAsmdef(destDir, asmdefName);
            }
            else if (existingAsmdef != null)
            {
                asmdefName = Path.GetFileNameWithoutExtension(existingAsmdef);
            }

            // 扫描引用此依赖的模块，添加引用
            var modules = FindModulesUsingDep(dep.id);
            foreach (var moduleAsmdef in modules)
            {
                if (asmdefName != null)
                    AddAsmdefReference(moduleAsmdef, asmdefName);
                if (hasDll)
                    AddDllReferences(moduleAsmdef, destDir);
            }
        }

        /// <summary>
        /// 卸载前处理：移除引用
        /// </summary>
        public static void OnUninstalling(DependencyDefinition dep, string destDir)
        {
            var existingAsmdef = Directory.Exists(destDir)
                ? Directory.GetFiles(destDir, "*.asmdef", SearchOption.TopDirectoryOnly).FirstOrDefault()
                : null;
            var asmdefName = existingAsmdef != null ? Path.GetFileNameWithoutExtension(existingAsmdef) : null;

            var modules = FindModulesUsingDep(dep.id);
            foreach (var moduleAsmdef in modules)
            {
                if (asmdefName != null)
                    RemoveAsmdefReference(moduleAsmdef, asmdefName);
                if (Directory.Exists(destDir))
                    RemoveDllReferences(moduleAsmdef, destDir);
            }
        }

        private static void CreateAsmdef(string destDir, string asmdefName)
        {
            var asmdefPath = Path.Combine(destDir, $"{asmdefName}.asmdef");
            var data = new AsmdefData { name = asmdefName, allowUnsafeCode = HasUnsafeCode(destDir) };
            File.WriteAllText(asmdefPath, JsonUtility.ToJson(data, true));
        }

        private static bool HasUnsafeCode(string dir)
        {
            foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(file);
                if (content.Contains("unsafe ") || content.Contains("unsafe{"))
                    return true;
            }
            return false;
        }

        private static List<string> FindModulesUsingDep(string depId)
        {
            var result = new List<string>();
            var modulesDir = Path.Combine(Application.dataPath, "Puffin/Modules");
            if (!Directory.Exists(modulesDir)) return result;

            foreach (var moduleDir in Directory.GetDirectories(modulesDir))
            {
                var depsFiles = Directory.GetFiles(moduleDir, "dependencies.json", SearchOption.AllDirectories);
                foreach (var depsFile in depsFiles)
                {
                    var config = DependencyConfig.LoadFromJson(depsFile);
                    if (config?.dependencies?.Any(d => d.id == depId) == true)
                    {
                        // 查找该模块的 Runtime asmdef
                        var runtimeDir = Path.Combine(moduleDir, "Runtime");
                        if (Directory.Exists(runtimeDir))
                        {
                            var asmdef = Directory.GetFiles(runtimeDir, "*.asmdef", SearchOption.TopDirectoryOnly).FirstOrDefault();
                            if (asmdef != null) result.Add(asmdef);
                        }
                    }
                }
            }
            return result;
        }

        private static void AddAsmdefReference(string moduleAsmdefPath, string targetAsmdefName)
        {
            AssetDatabase.Refresh();
            var guid = GetAsmdefGuid(targetAsmdefName);
            if (string.IsNullOrEmpty(guid)) return;

            var json = File.ReadAllText(moduleAsmdefPath);
            var data = JsonUtility.FromJson<AsmdefData>(json);
            var refName = $"GUID:{guid}";
            if (!data.references.Contains(refName))
            {
                data.references.Add(refName);
                File.WriteAllText(moduleAsmdefPath, JsonUtility.ToJson(data, true));
            }
        }

        private static void RemoveAsmdefReference(string moduleAsmdefPath, string targetAsmdefName)
        {
            var guid = GetAsmdefGuid(targetAsmdefName);
            if (string.IsNullOrEmpty(guid)) return;

            var json = File.ReadAllText(moduleAsmdefPath);
            var data = JsonUtility.FromJson<AsmdefData>(json);
            if (data.references.Remove($"GUID:{guid}"))
                File.WriteAllText(moduleAsmdefPath, JsonUtility.ToJson(data, true));
        }

        private static void AddDllReferences(string moduleAsmdefPath, string destDir)
        {
            var dlls = Directory.GetFiles(destDir, "*.dll", SearchOption.AllDirectories)
                .Select(Path.GetFileName).ToList();
            if (dlls.Count == 0) return;

            var json = File.ReadAllText(moduleAsmdefPath);
            var data = JsonUtility.FromJson<AsmdefData>(json);
            var changed = false;

            foreach (var dll in dlls)
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
                File.WriteAllText(moduleAsmdefPath, JsonUtility.ToJson(data, true));
            }
        }

        private static void RemoveDllReferences(string moduleAsmdefPath, string destDir)
        {
            var dlls = Directory.GetFiles(destDir, "*.dll", SearchOption.AllDirectories)
                .Select(Path.GetFileName).ToList();
            if (dlls.Count == 0) return;

            var json = File.ReadAllText(moduleAsmdefPath);
            var data = JsonUtility.FromJson<AsmdefData>(json);
            var changed = false;

            foreach (var dll in dlls)
            {
                if (data.precompiledReferences.Remove(dll))
                    changed = true;
            }

            if (changed)
            {
                if (data.precompiledReferences.Count == 0)
                    data.overrideReferences = false;
                File.WriteAllText(moduleAsmdefPath, JsonUtility.ToJson(data, true));
            }
        }

        private static string GetAsmdefGuid(string asmdefName)
        {
            var guids = AssetDatabase.FindAssets($"t:AssemblyDefinitionAsset {asmdefName}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == asmdefName)
                    return guid;
            }
            return "";
        }
    }
}
#endif
