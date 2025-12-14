#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Puffin.Editor.Environment.Core;
using Puffin.Editor.Environment.Installers;
using UnityEngine;

namespace Puffin.Editor.Environment
{
    public class DependencyManager
    {
        public static string SharedPluginsDir => Path.Combine(Application.dataPath, "Plugins/Puffin");
        public static string CacheDir => Path.Combine(Application.dataPath, "../Library/PuffinCache");

        private readonly Dictionary<DependencySource, IPackageInstaller> _installers;
        private readonly Downloader _downloader;

        public event Action<string> OnStatusChanged;
        public event Action<float, long, long> OnProgress;

        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        public DependencyManager()
        {
            _downloader = new Downloader();
            _downloader.OnProgress += (p, d, t, s) => OnProgress?.Invoke(p, d, t);

            _installers = new Dictionary<DependencySource, IPackageInstaller>
            {
                {DependencySource.NuGet, new NuGetInstaller()},
                {DependencySource.GitHubRepo, new GitHubRepoInstaller()},
                {DependencySource.GitHubRelease, new GitHubReleaseInstaller()},
                {DependencySource.DirectUrl, new DirectUrlInstaller()}
            };
        }

        public string GetInstallPath(DependencyDefinition dep)
        {
            // 如果有自定义安装目录，使用项目根目录 + installDir
            if (!string.IsNullOrEmpty(dep.installDir))
                return Path.GetFullPath(Path.Combine(ProjectRoot, dep.installDir));
            return Path.Combine(SharedPluginsDir, dep.id);
        }

        public bool IsInstalled(DependencyDefinition dep)
        {
            if (!_installers.TryGetValue(dep.source, out var installer))
                return false;
            return installer.IsInstalled(dep, GetInstallPath(dep));
        }

        public async UniTask<bool> InstallAsync(DependencyDefinition dep, CancellationToken ct = default)
        {
            if (!_installers.TryGetValue(dep.source, out var installer))
            {
                OnStatusChanged?.Invoke($"不支持的安装源: {dep.source}");
                return false;
            }

            OnStatusChanged?.Invoke($"正在安装 {dep.displayName ?? dep.id}...");

            // 先安装依赖
            if (dep.dependencies != null)
            {
                foreach (var d in dep.dependencies)
                {
                    if (!IsInstalled(d))
                    {
                        if (!await InstallAsync(d, ct))
                            return false;
                    }
                }
            }

            var destDir = GetInstallPath(dep);
            return await installer.InstallAsync(dep, destDir, _downloader, ct);
        }

        public async UniTask<bool> InstallFromCacheAsync(DependencyDefinition dep, string cachePath, CancellationToken ct = default)
        {
            if (!_installers.TryGetValue(dep.source, out var installer))
            {
                OnStatusChanged?.Invoke($"不支持的安装源: {dep.source}");
                return false;
            }

            OnStatusChanged?.Invoke($"正在安装 {dep.displayName ?? dep.id}...");
            var destDir = GetInstallPath(dep);
            return await installer.InstallFromCacheAsync(dep, destDir, cachePath, ct);
        }


        /// <summary>
        /// 获取缓存文件路径
        /// </summary>
        public static string GetCachePath(string id, string version, string ext)
        {
            var cacheDir = Path.GetFullPath(CacheDir);
            Directory.CreateDirectory(cacheDir);
            var fileName = string.IsNullOrEmpty(version) ? $"{id}{ext}" : $"{id}_{version}{ext}";
            return Path.Combine(cacheDir, fileName);
        }

        /// <summary>
        /// 卸载依赖
        /// </summary>
        public bool Uninstall(DependencyDefinition dep)
        {
            var path = GetInstallPath(dep);
            if (!Directory.Exists(path)) return false;
            try
            {
                Directory.Delete(path, true);
                // 删除对应的 .meta 文件
                var metaPath = path + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 清理缓存
        /// </summary>
        public static void ClearCache()
        {
            if (Directory.Exists(CacheDir))
                Directory.Delete(CacheDir, true);
        }
    }
}
#endif