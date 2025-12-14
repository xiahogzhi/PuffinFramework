#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Puffin.Editor.Environment.Core;

namespace Puffin.Editor.Environment.Installers
{
    public class NuGetInstaller : IPackageInstaller
    {
        public DependencySource SupportedSource => DependencySource.NuGet;

        public async UniTask<bool> InstallAsync(DependencyDefinition dep, string destDir, Downloader downloader, CancellationToken ct = default)
        {
            var (packageName, version) = ParseUrl(dep.url, dep.version);
            var cachePath = DependencyManager.GetCachePath(packageName, version, ".nupkg");
            var tempDir = Path.Combine(Path.GetTempPath(), $"nuget_{Guid.NewGuid()}");

            try
            {
                Directory.CreateDirectory(destDir);

                // 使用缓存或下载
                if (!File.Exists(cachePath))
                {
                    var url = $"https://www.nuget.org/api/v2/package/{packageName}/{version}";
                    if (!await downloader.DownloadAsync(url, cachePath, ct))
                        return false;
                }

                var extractDir = Path.Combine(tempDir, "extracted");
                if (!Extractor.ExtractZip(cachePath, extractDir))
                    return false;

                var frameworks = dep.targetFrameworks ?? new[] { "netstandard2.0", "netstandard2.1", "netstandard1.1" };
                string dllPath = null;
                foreach (var fw in frameworks)
                {
                    var path = Path.Combine(extractDir, "lib", fw, $"{packageName}.dll");
                    if (File.Exists(path))
                    {
                        dllPath = path;
                        break;
                    }
                }

                if (dllPath != null)
                {
                    File.Copy(dllPath, Path.Combine(destDir, $"{packageName}.dll"), true);
                    return true;
                }

                return false;
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }

        public UniTask<bool> InstallFromCacheAsync(DependencyDefinition dep, string destDir, string cachePath, CancellationToken ct = default)
        {
            var (packageName, _) = ParseUrl(dep.url, dep.version);
            var tempDir = Path.Combine(Path.GetTempPath(), $"nuget_{Guid.NewGuid()}");
            try
            {
                Directory.CreateDirectory(destDir);
                var extractDir = Path.Combine(tempDir, "extracted");
                if (!Extractor.ExtractZip(cachePath, extractDir))
                    return UniTask.FromResult(false);

                var frameworks = dep.targetFrameworks ?? new[] { "netstandard2.0", "netstandard2.1", "netstandard1.1" };
                foreach (var fw in frameworks)
                {
                    var path = Path.Combine(extractDir, "lib", fw, $"{packageName}.dll");
                    if (File.Exists(path))
                    {
                        File.Copy(path, Path.Combine(destDir, $"{packageName}.dll"), true);
                        return UniTask.FromResult(true);
                    }
                }
                return UniTask.FromResult(false);
            }
            catch
            {
                return UniTask.FromResult(false);
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }

        public bool IsInstalled(DependencyDefinition dep, string destDir)
        {
            if (dep.requiredFiles != null)
            {
                foreach (var file in dep.requiredFiles)
                {
                    if (File.Exists(Path.Combine(destDir, file)))
                        return true;
                }
            }
            var (packageName, _) = ParseUrl(dep.url, dep.version);
            return File.Exists(Path.Combine(destDir, $"{packageName}.dll"));
        }

        private static (string name, string version) ParseUrl(string url, string defaultVersion)
        {
            if (url.Contains("@"))
            {
                var parts = url.Split('@');
                return (parts[0], parts[1]);
            }
            return (url, defaultVersion ?? "latest");
        }
    }
}
#endif
