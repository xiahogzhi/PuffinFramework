#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Puffin.Editor.Environment.Core;
using UnityEngine;

namespace Puffin.Editor.Environment.Installers
{
    /// <summary>
    /// 手动导入依赖检查器
    /// 用于检查用户是否已手动导入所需插件（如 Odin, DOTween 等）
    /// 不执行实际安装，仅验证依赖是否存在
    /// </summary>
    public class ManualImportInstaller : IPackageInstaller
    {
        public DependencySource SupportedSource => DependencySource.ManualImport;

        public UniTask<bool> InstallAsync(DependencyDefinition dep, string destDir, Downloader downloader, CancellationToken ct = default)
        {
            // ManualImport 类型不执行安装，只检查是否已存在
            if (!IsInstalled(dep, destDir))
            {
                Debug.LogError($"[ManualImportInstaller] 缺少必需的插件: {dep.displayName ?? dep.id}\n" +
                               $"请先手动导入该插件后再安装此模块。\n" +
                               $"检查条件: {GetCheckDescription(dep)}");
                return UniTask.FromResult(false);
            }

            Debug.Log($"[ManualImportInstaller] 已检测到插件: {dep.displayName ?? dep.id}");
            return UniTask.FromResult(true);
        }

        public UniTask<bool> InstallFromCacheAsync(DependencyDefinition dep, string destDir, string cachePath, CancellationToken ct = default)
        {
            return InstallAsync(dep, destDir, null, ct);
        }

        public bool IsInstalled(DependencyDefinition dep, string destDir)
        {
            // 检查 asmdefName（程序集定义）
            if (!string.IsNullOrEmpty(dep.asmdefName))
            {
                if (FindAsmdef(dep.asmdefName))
                    return true;
            }

            // 检查 dllReferences（DLL 文件）
            if (dep.dllReferences != null && dep.dllReferences.Length > 0)
            {
                if (dep.dllReferences.All(dll => FindDll(dll)))
                    return true;
            }

            // 检查 requiredFiles（必需文件）
            if (dep.requiredFiles != null && dep.requiredFiles.Length > 0)
            {
                return dep.requiredFiles.All(file => FileExists(file));
            }

            // 如果没有指定检查条件，检查 id 作为 asmdef 或 dll 名称
            if (!string.IsNullOrEmpty(dep.id))
            {
                return FindAsmdef(dep.id) || FindDll(dep.id);
            }

            return false;
        }

        private bool FindAsmdef(string asmdefName)
        {
            var assetsPath = Application.dataPath;
            var packagesPath = Path.Combine(Application.dataPath, "../Packages");

            // 在 Assets 目录搜索
            if (SearchAsmdefInDirectory(assetsPath, asmdefName))
                return true;

            // 在 Packages 目录搜索（Library/PackageCache 中的包）
            var packageCachePath = Path.Combine(Application.dataPath, "../Library/PackageCache");
            if (Directory.Exists(packageCachePath) && SearchAsmdefInDirectory(packageCachePath, asmdefName))
                return true;

            // 检查 Packages/manifest.json 中的本地包
            if (Directory.Exists(packagesPath) && SearchAsmdefInDirectory(packagesPath, asmdefName))
                return true;

            return false;
        }

        private bool SearchAsmdefInDirectory(string directory, string asmdefName)
        {
            if (!Directory.Exists(directory))
                return false;

            try
            {
                var asmdefFiles = Directory.GetFiles(directory, "*.asmdef", SearchOption.AllDirectories);
                foreach (var file in asmdefFiles)
                {
                    // 跳过带 ~ 的目录
                    if (file.Contains("~")) continue;
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (fileName.Equals(asmdefName, System.StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch (System.Exception)
            {
                // 忽略访问权限等错误
            }

            return false;
        }

        private bool FindDll(string dllName)
        {
            var assetsPath = Application.dataPath;
            var packageCachePath = Path.Combine(Application.dataPath, "../Library/PackageCache");
            var packagesPath = Path.Combine(Application.dataPath, "../Packages");

            // 确保有 .dll 扩展名
            if (!dllName.EndsWith(".dll", System.StringComparison.OrdinalIgnoreCase))
                dllName += ".dll";

            // 在 Assets 目录搜索
            if (SearchFileInDirectory(assetsPath, dllName))
                return true;

            // 在 PackageCache 目录搜索
            if (Directory.Exists(packageCachePath) && SearchFileInDirectory(packageCachePath, dllName))
                return true;

            // 在 Packages 目录搜索
            if (Directory.Exists(packagesPath) && SearchFileInDirectory(packagesPath, dllName))
                return true;

            return false;
        }

        private bool SearchFileInDirectory(string directory, string fileName)
        {
            if (!Directory.Exists(directory))
                return false;

            try
            {
                var files = Directory.GetFiles(directory, fileName, SearchOption.AllDirectories);
                // 跳过带 ~ 的目录
                return files.Any(f => !f.Contains("~"));
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private bool FileExists(string relativePath)
        {
            var fullPath = Path.Combine(Application.dataPath, relativePath);
            return File.Exists(fullPath) || Directory.Exists(fullPath);
        }

        private string GetCheckDescription(DependencyDefinition dep)
        {
            var checks = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrEmpty(dep.asmdefName))
                checks.Add($"程序集定义 '{dep.asmdefName}'");

            if (dep.dllReferences != null && dep.dllReferences.Length > 0)
                checks.Add($"DLL [{string.Join(", ", dep.dllReferences)}]");

            if (dep.requiredFiles != null && dep.requiredFiles.Length > 0)
                checks.Add($"文件 [{string.Join(", ", dep.requiredFiles)}]");

            if (checks.Count == 0)
                checks.Add($"程序集/DLL '{dep.id}'");

            return string.Join(" 或 ", checks);
        }
    }
}
#endif
