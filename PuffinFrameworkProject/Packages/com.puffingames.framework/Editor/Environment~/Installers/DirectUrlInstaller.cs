#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Puffin.Editor.Environment.Core;

namespace Puffin.Editor.Environment.Installers
{
    /// <summary>
    /// 直接 URL 安装器，从指定 URL 下载文件或压缩包
    /// </summary>
    public class DirectUrlInstaller : IPackageInstaller
    {
        public DependencySource SupportedSource => DependencySource.DirectUrl;

        public async UniTask<bool> InstallAsync(DependencyDefinition dep, string destDir, Downloader downloader, CancellationToken ct = default)
        {
            var url = dep.url;
            var ext = Path.GetExtension(url).ToLowerInvariant();
            var isArchive = ext is ".zip" or ".7z";
            var cachePath = DependencyManager.GetCachePath(dep.id, dep.version, ext);

            Directory.CreateDirectory(destDir);

            if (isArchive)
            {
                try
                {
                    if (!File.Exists(cachePath))
                    {
                        if (!await downloader.DownloadAsync(url, cachePath, ct))
                            return false;
                    }

                    var tempDir = Path.Combine(Path.GetTempPath(), $"direct_{Guid.NewGuid()}");
                    try
                    {
                        var extractDir = Path.Combine(tempDir, "extracted");
                        if (!Extractor.Extract(cachePath, extractDir))
                            return false;

                        var srcDir = extractDir;
                        if (!string.IsNullOrEmpty(dep.extractPath))
                        {
                            var dirs = Directory.GetDirectories(extractDir);
                            var rootDir = dirs.Length > 0 ? dirs[0] : extractDir;
                            srcDir = Path.Combine(rootDir, dep.extractPath);
                        }

                        if (Directory.Exists(srcDir))
                            CopyDirectory(srcDir, destDir);

                        return true;
                    }
                    finally
                    {
                        try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                    }
                }
                catch
                {
                    return false;
                }
            }

            // Direct file download (e.g., .exe)
            if (!File.Exists(cachePath))
            {
                if (!await downloader.DownloadAsync(url, cachePath, ct))
                    return false;
            }

            var fileName = Path.GetFileName(url);
            File.Copy(cachePath, Path.Combine(destDir, fileName), true);
            return true;
        }

        public UniTask<bool> InstallFromCacheAsync(DependencyDefinition dep, string destDir, string cachePath, CancellationToken ct = default)
        {
            var ext = Path.GetExtension(cachePath).ToLowerInvariant();
            var isArchive = ext is ".zip" or ".7z";
            try
            {
                Directory.CreateDirectory(destDir);
                if (isArchive)
                {
                    var tempDir = Path.Combine(Path.GetTempPath(), $"direct_{Guid.NewGuid()}");
                    try
                    {
                        var extractDir = Path.Combine(tempDir, "extracted");
                        if (!Extractor.Extract(cachePath, extractDir))
                            return UniTask.FromResult(false);

                        var srcDir = extractDir;
                        if (!string.IsNullOrEmpty(dep.extractPath))
                        {
                            var dirs = Directory.GetDirectories(extractDir);
                            var rootDir = dirs.Length > 0 ? dirs[0] : extractDir;
                            srcDir = Path.Combine(rootDir, dep.extractPath);
                        }
                        if (Directory.Exists(srcDir))
                            CopyDirectory(srcDir, destDir);
                        return UniTask.FromResult(true);
                    }
                    finally
                    {
                        try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                    }
                }
                var fileName = Path.GetFileName(dep.url);
                File.Copy(cachePath, Path.Combine(destDir, fileName), true);
                return UniTask.FromResult(true);
            }
            catch
            {
                return UniTask.FromResult(false);
            }
        }

        public bool IsInstalled(DependencyDefinition dep, string destDir)
        {
            if (!Directory.Exists(destDir))
                return false;

            if (dep.requiredFiles != null)
            {
                foreach (var file in dep.requiredFiles)
                {
                    if (File.Exists(Path.Combine(destDir, file)))
                        return true;
                }
                return false;
            }

            return Directory.GetFiles(destDir, "*.*", SearchOption.AllDirectories).Length > 0;
        }

        private static void CopyDirectory(string src, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(src))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(src))
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }
    }
}
#endif
