#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Puffin.Editor.Environment
{
    public class GitHubRepoInstaller : IPackageInstaller
    {
        public DependencySource SupportedSource => DependencySource.GitHubRepo;

        public async UniTask<bool> InstallAsync(DependencyDefinition dep, string destDir, Downloader downloader, CancellationToken ct = default)
        {
            var (owner, repo, branch) = ParseUrl(dep.url);
            var cachePath = DependencyManager.GetCachePath($"{owner}_{repo}", branch, ".zip");
            var tempDir = Path.Combine(Path.GetTempPath(), $"github_{Guid.NewGuid()}");

            try
            {
                // 使用缓存或下载（检查缓存文件是否有效）
                var cacheValid = File.Exists(cachePath) && new FileInfo(cachePath).Length > 1024;
                if (!cacheValid)
                {
                    if (File.Exists(cachePath)) File.Delete(cachePath);
                    var url = $"https://github.com/{owner}/{repo}/archive/refs/heads/{branch}.zip";
                    Debug.Log($"[GitHubRepoInstaller] 开始下载: {url}");
                    if (!await downloader.DownloadAsync(url, cachePath, ct))
                    {
                        Debug.LogError($"[GitHubRepoInstaller] 下载失败: {url}");
                        return false;
                    }
                }
                else
                {
                    Debug.Log($"[GitHubRepoInstaller] 使用缓存: {cachePath}");
                }

                // 确认文件存在且大小正确
                if (!File.Exists(cachePath))
                {
                    Debug.LogError($"[GitHubRepoInstaller] 下载后文件不存在: {cachePath}");
                    return false;
                }
                var fileSize = new FileInfo(cachePath).Length;
                Debug.Log($"[GitHubRepoInstaller] 文件大小: {fileSize / 1024f:F0} KB, 开始解压...");

                var extractDir = Path.Combine(tempDir, "extracted");
                if (!Extractor.ExtractZip(cachePath, extractDir))
                {
                    Debug.LogError($"[GitHubRepoInstaller] 解压失败: {cachePath}");
                    return false;
                }

                var dirs = Directory.GetDirectories(extractDir);
                var rootDir = dirs.Length > 0 ? dirs[0] : extractDir;

                var srcDir = string.IsNullOrEmpty(dep.extractPath)
                    ? rootDir
                    : Path.Combine(rootDir, dep.extractPath);

                if (!Directory.Exists(srcDir))
                {
                    Debug.LogError($"[GitHubRepoInstaller] 源目录不存在: {srcDir}");
                    return false;
                }

                Directory.CreateDirectory(destDir);
                var pattern = dep.type == DependencyType.Source ? "*.cs" : "*.*";
                CopyDirectory(srcDir, destDir, pattern);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GitHubRepoInstaller] 安装异常: {e}");
                return false;
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }

        public UniTask<bool> InstallFromCacheAsync(DependencyDefinition dep, string destDir, string cachePath, CancellationToken ct = default)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"github_{Guid.NewGuid()}");
            try
            {
                var extractDir = Path.Combine(tempDir, "extracted");
                if (!Extractor.ExtractZip(cachePath, extractDir))
                    return UniTask.FromResult(false);

                var dirs = Directory.GetDirectories(extractDir);
                var rootDir = dirs.Length > 0 ? dirs[0] : extractDir;
                var srcDir = string.IsNullOrEmpty(dep.extractPath) ? rootDir : Path.Combine(rootDir, dep.extractPath);

                if (!Directory.Exists(srcDir))
                    return UniTask.FromResult(false);

                Directory.CreateDirectory(destDir);
                var pattern = dep.type == DependencyType.Source ? "*.cs" : "*.*";
                CopyDirectory(srcDir, destDir, pattern);
                return UniTask.FromResult(true);
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

            var pattern = dep.type == DependencyType.Source ? "*.cs" : "*.dll";
            return Directory.GetFiles(destDir, pattern, SearchOption.AllDirectories).Length > 0;
        }

        private static (string owner, string repo, string branch) ParseUrl(string url)
        {
            var branch = "master";
            if (url.Contains("@"))
            {
                var parts = url.Split('@');
                url = parts[0];
                branch = parts[1];
            }
            var segments = url.Split('/');
            return (segments[0], segments[1], branch);
        }

        private static void CopyDirectory(string src, string dest, string pattern)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(src, pattern))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(src))
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)), pattern);
        }
    }
}
#endif
