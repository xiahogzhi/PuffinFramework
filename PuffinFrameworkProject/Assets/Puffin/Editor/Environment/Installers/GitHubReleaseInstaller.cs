#if UNITY_EDITOR
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Puffin.Editor.Environment.Core;

namespace Puffin.Editor.Environment.Installers
{
    public class GitHubReleaseInstaller : IPackageInstaller
    {
        public DependencySource SupportedSource => DependencySource.GitHubRelease;

        public async UniTask<bool> InstallAsync(DependencyDefinition dep, string destDir, Downloader downloader, CancellationToken ct = default)
        {
            var ext = Path.GetExtension(dep.url).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = ".7z";
            var cachePath = DependencyManager.GetCachePath(dep.id, dep.version, ext);

            try
            {
                // 使用缓存或下载
                if (!File.Exists(cachePath))
                {
                    if (!await downloader.DownloadAsync(dep.url, cachePath, ct))
                        return false;
                }

                Directory.CreateDirectory(destDir);

                // 根据扩展名解压或直接复制
                if (ext is ".7z" or ".zip")
                {
                    // 如果是 7z 且 7z 工具不可用，先安装
                    if (ext == ".7z" && !Extractor.Is7zAvailable())
                    {
                        if (!await Extractor.Install7zAsync(downloader))
                            return false;
                    }

                    if (!Extractor.Extract(cachePath, destDir))
                        return false;
                }
                else
                {
                    // 直接复制文件（如 .exe）
                    var fileName = Path.GetFileName(dep.url);
                    File.Copy(cachePath, Path.Combine(destDir, fileName), true);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public UniTask<bool> InstallFromCacheAsync(DependencyDefinition dep, string destDir, string cachePath, CancellationToken ct = default)
        {
            var ext = Path.GetExtension(cachePath).ToLowerInvariant();
            try
            {
                Directory.CreateDirectory(destDir);
                if (ext is ".7z" or ".zip")
                {
                    if (!Extractor.Extract(cachePath, destDir))
                        return UniTask.FromResult(false);
                }
                else
                {
                    var fileName = Path.GetFileName(dep.url);
                    File.Copy(cachePath, Path.Combine(destDir, fileName), true);
                }
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
    }
}
#endif
