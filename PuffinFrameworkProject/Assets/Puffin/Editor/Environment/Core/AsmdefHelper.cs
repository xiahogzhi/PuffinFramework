// #if UNITY_EDITOR
// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using UnityEngine;
//
// namespace Puffin.Editor.Environment.Core
// {
//     /// <summary>
//     /// 程序集定义文件（.asmdef）辅助工具
//     /// </summary>
//     public static class AsmdefHelper
//     {
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
//             public List<object> versionDefines = new();
//             public bool noEngineReferences;
//         }
//
//         /// <summary>
//         /// 安装后处理：检测并创建 asmdef（不再自动添加引用，引用由模块配置管理）
//         /// </summary>
//         public static void OnInstalled(DependencyDefinition dep, string destDir)
//         {
//             var hasCs = Directory.GetFiles(destDir, "*.cs", SearchOption.AllDirectories).Length > 0;
//             var existingAsmdef =
//                 Directory.GetFiles(destDir, "*.asmdef", SearchOption.TopDirectoryOnly).FirstOrDefault();
//
//             // 有代码但没有 asmdef，创建一个
//             if (hasCs && existingAsmdef == null && !string.IsNullOrEmpty(dep.asmdefName))
//             {
//                 var asmdefName = dep.asmdefName ?? dep.id.Replace(".", "_");
//                 CreateAsmdef(destDir, asmdefName);
//             }
//         }
//
//         /// <summary>
//         /// 卸载前处理（引用由模块配置管理，此处不再处理）
//         /// </summary>
//         public static void OnUninstalling(DependencyDefinition dep, string destDir)
//         {
//             // 引用由模块的 references 配置管理，不再在此处理
//         }
//
//         private static void CreateAsmdef(string destDir, string asmdefName)
//         {
//             var asmdefPath = Path.Combine(destDir, $"{asmdefName}.asmdef");
//             var data = new AsmdefData {name = asmdefName, allowUnsafeCode = HasUnsafeCode(destDir)};
//             File.WriteAllText(asmdefPath, JsonUtility.ToJson(data, true));
//         }
//
//         private static bool HasUnsafeCode(string dir)
//         {
//             foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
//             {
//                 var content = File.ReadAllText(file);
//                 if (content.Contains("unsafe ") || content.Contains("unsafe{"))
//                     return true;
//             }
//
//             return false;
//         }
//     }
// }
// #endif