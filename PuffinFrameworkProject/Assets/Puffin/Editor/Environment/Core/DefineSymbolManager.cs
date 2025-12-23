// #if UNITY_EDITOR
// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using Puffin.Editor.Hub.Data;
// using Puffin.Editor.Hub.Services;
// using UnityEditor;
// using UnityEngine;
//
// namespace Puffin.Editor.Environment.Core
// {
//     /// <summary>
//     /// 宏定义管理器，通过 asmdef 的 versionDefines 为模块添加 {ID}_INSTALLED 宏
//     /// 只影响当前模块的程序集，不修改全局宏定义
//     /// </summary>
//     [InitializeOnLoad]
//     public static class DefineSymbolManager
//     {
//         private const string SUFFIX = "_INSTALLED";
//         private static bool _isResolving;
//
//         [Serializable]
//         private class VersionDefine
//         {
//             public string name = "";
//             public string expression = "";
//             public string define = "";
//         }
//
//         [Serializable]
//         private class AsmdefData
//         {
//             public string name = "";
//             public string rootNamespace = "";
//             public List<string> references = new();
//             public List<string> includePlatforms = new();
//             public List<string> excludePlatforms = new();
//             public bool allowUnsafeCode;
//             public bool overrideReferences;
//             public List<string> precompiledReferences = new();
//             public bool autoReferenced = true;
//             public List<string> defineConstraints = new();
//             public List<VersionDefine> versionDefines = new();
//             public bool noEngineReferences;
//         }
//
//         static DefineSymbolManager()
//         {
//             EditorApplication.delayCall += () => RefreshAll();
//             EditorApplication.focusChanged += f => { if (f) RefreshAll(); };
//         }
//
//         public static void RefreshAll()
//         {
//             if (_isResolving) return;
//             _isResolving = true;
//
//             try
//             {
//                 var modulesDir = ManifestService.GetModulesPath();
//                 if (!Directory.Exists(modulesDir)) return;
//
//                 foreach (var dir in Directory.GetDirectories(modulesDir))
//                 {
//                     if (dir.Contains("~")) continue;
//                     ProcessModule(dir);
//                 }
//             }
//             finally
//             {
//                 _isResolving = false;
//             }
//         }
//
//         private static void ProcessModule(string moduleDir)
//         {
//             var manifestPath = ManifestService.GetManifestPathFromDir(moduleDir);
//             if (!File.Exists(manifestPath)) return;
//
//             try
//             {
//                 var manifest = ManifestService.Load(manifestPath);
//                 var versionDefines = BuildVersionDefines(manifest?.envDependencies);
//
//                 // 更新 Runtime 和 Editor 的 asmdef
//                 UpdateAsmdef(Path.Combine(moduleDir, "Runtime"), versionDefines);
//                 UpdateAsmdef(Path.Combine(moduleDir, "Editor"), versionDefines);
//             }
//             catch (Exception e)
//             {
//                 Debug.LogWarning($"[DefineSymbolManager] {Path.GetFileName(moduleDir)}: {e.Message}");
//             }
//         }
//
//         private static List<VersionDefine> BuildVersionDefines(EnvironmentDependency[] envDeps)
//         {
//             if (envDeps == null || envDeps.Length == 0) return new List<VersionDefine>();
//
//             var defines = new List<VersionDefine>();
//             foreach (var dep in envDeps)
//             {
//                 // 使用 asmdefName 或 id 作为检测名称
//                 var detectName = !string.IsNullOrEmpty(dep.asmdefName) ? dep.asmdefName : dep.id;
//                 var symbol = NormalizeSymbol(dep.id);
//
//                 defines.Add(new VersionDefine
//                 {
//                     name = detectName,
//                     expression = "",
//                     define = symbol
//                 });
//             }
//             return defines;
//         }
//
//         private static void UpdateAsmdef(string dir, List<VersionDefine> newDefines)
//         {
//             if (!Directory.Exists(dir)) return;
//
//             var asmdefPath = Directory.GetFiles(dir, "*.asmdef", SearchOption.TopDirectoryOnly)
//                 .FirstOrDefault(f => !f.Contains("~"));
//             if (asmdefPath == null) return;
//
//             var json = File.ReadAllText(asmdefPath);
//             var data = JsonUtility.FromJson<AsmdefData>(json);
//
//             // 保留非 _INSTALLED 的 versionDefines
//             var preserved = data.versionDefines?.Where(v => !v.define.EndsWith(SUFFIX)).ToList()
//                             ?? new List<VersionDefine>();
//
//             var merged = preserved.Concat(newDefines).ToList();
//
//             // 检查是否有变化
//             var oldDefines = data.versionDefines ?? new List<VersionDefine>();
//             if (AreVersionDefinesEqual(oldDefines, merged)) return;
//
//             data.versionDefines = merged;
//             File.WriteAllText(asmdefPath, JsonUtility.ToJson(data, true));
//         }
//
//         private static bool AreVersionDefinesEqual(List<VersionDefine> a, List<VersionDefine> b)
//         {
//             if (a.Count != b.Count) return false;
//             var setA = a.Select(v => $"{v.name}|{v.define}").OrderBy(x => x);
//             var setB = b.Select(v => $"{v.name}|{v.define}").OrderBy(x => x);
//             return setA.SequenceEqual(setB);
//         }
//
//         private static string NormalizeSymbol(string id)
//         {
//             var normalized = id.Replace(".", "_").Replace("-", "_").Replace(" ", "_").ToUpperInvariant();
//             return normalized + SUFFIX;
//         }
//     }
// }
// #endif
