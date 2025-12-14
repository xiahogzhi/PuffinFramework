#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Environment.Core
{
    public enum TaskState { Downloading, Downloaded, Installing, Completed, Failed, Cancelled }

    [InitializeOnLoad]
    public static class DownloadService
    {
        public class DownloadTask
        {
            public string Id;
            public DependencyDefinition Dep;
            public float Progress;
            public long Downloaded;
            public long Total;
            public long Speed;
            public TaskState State;
            public string Error;
            public CancellationTokenSource Cts;
            public string CachePath;
            public bool IsRunning => State == TaskState.Downloading || State == TaskState.Installing;
        }

        private static readonly Dictionary<string, DownloadTask> _tasks = new();
        private static readonly Queue<DownloadTask> _installQueue = new();
        private static bool _isInstalling;
        public static event Action OnTasksChanged;

        static DownloadService()
        {
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            // 检查是否有待安装的任务
            if (!_isInstalling && _installQueue.Count > 0)
            {
                var task = _installQueue.Dequeue();
                if (task.State == TaskState.Downloaded)
                    RunInstallAsync(task).Forget();
            }
        }

        public static IReadOnlyDictionary<string, DownloadTask> Tasks => _tasks;

        public static bool IsDownloading(string depId) =>
            _tasks.TryGetValue(depId, out var t) && t.IsRunning;

        public static DownloadTask GetTask(string depId) =>
            _tasks.TryGetValue(depId, out var t) ? t : null;

        public static bool HasCache(DependencyDefinition dep)
        {
            var cachePath = GetCachePathForDep(dep);
            return File.Exists(cachePath) && new FileInfo(cachePath).Length > 1024;
        }

        public static void DeleteCache(DependencyDefinition dep)
        {
            var cachePath = GetCachePathForDep(dep);
            try { if (File.Exists(cachePath)) File.Delete(cachePath); } catch { }
            _tasks.Remove(dep.id);
            OnTasksChanged?.Invoke();
        }

        public static void StartInstallFromCache(DependencyDefinition dep)
        {
            if (_tasks.TryGetValue(dep.id, out var existing) && existing.IsRunning)
                return;

            var cachePath = GetCachePathForDep(dep);
            if (!File.Exists(cachePath))
                return;

            var task = new DownloadTask
            {
                Id = dep.id,
                Dep = dep,
                Progress = 1f,
                State = TaskState.Downloaded,
                Cts = new CancellationTokenSource(),
                CachePath = cachePath
            };
            _tasks[dep.id] = task;
            _installQueue.Enqueue(task);
            OnTasksChanged?.Invoke();
        }

        public static void StartDownload(DependencyDefinition dep)
        {
            if (_tasks.TryGetValue(dep.id, out var existing))
            {
                if (existing.IsRunning || existing.State == TaskState.Downloaded)
                    return;
                _tasks.Remove(dep.id);
            }

            var task = new DownloadTask
            {
                Id = dep.id,
                Dep = dep,
                Progress = 0.01f,
                State = TaskState.Downloading,
                Cts = new CancellationTokenSource()
            };
            _tasks[dep.id] = task;
            OnTasksChanged?.Invoke();

            RunDownloadAsync(task).Forget();
        }

        public static void CancelDownload(string depId)
        {
            if (_tasks.TryGetValue(depId, out var task))
            {
                task.Cts?.Cancel();
                task.State = TaskState.Cancelled;
                _tasks.Remove(depId);
                OnTasksChanged?.Invoke();
            }
        }

        private static async UniTaskVoid RunDownloadAsync(DownloadTask task)
        {
            Debug.Log($"[DownloadService] 开始下载: {task.Dep.displayName ?? task.Dep.id}");

            var downloader = new Downloader();
            downloader.OnProgress += (p, d, t, s) =>
            {
                task.Progress = p;
                task.Downloaded = d;
                task.Total = t;
                task.Speed = s;
                EditorApplication.delayCall += () => OnTasksChanged?.Invoke();
            };

            try
            {
                // 获取缓存路径
                var dep = task.Dep;
                var cachePath = GetCachePathForDep(dep);
                task.CachePath = cachePath;

                // 检查缓存是否有效
                var cacheValid = File.Exists(cachePath) && new FileInfo(cachePath).Length > 1024;
                if (cacheValid)
                {
                    Debug.Log($"[DownloadService] 使用缓存: {cachePath}");
                    task.State = TaskState.Downloaded;
                    task.Progress = 1f;
                }
                else
                {
                    if (File.Exists(cachePath)) File.Delete(cachePath);

                    var url = GetDownloadUrl(dep);
                    
                    if (string.IsNullOrEmpty(url))
                    {
                        task.State = TaskState.Failed;
                        task.Error = "无法获取下载地址:" + url;
                        return;
                    }
                    Debug.Log($"[DownloadService] 请求地址: {url}");
                    var success = await downloader.DownloadAsync(url, cachePath, task.Cts.Token);
                    if (!success || !File.Exists(cachePath) || new FileInfo(cachePath).Length < 1024)
                    {
                        task.State = TaskState.Failed;
                        task.Error = "下载失败";
                        return;
                    }
                    task.State = TaskState.Downloaded;
                }

                Debug.Log($"[DownloadService] 下载完成: {task.Dep.displayName ?? task.Dep.id}");
                EditorApplication.delayCall += () => OnTasksChanged?.Invoke();
            }
            catch (OperationCanceledException)
            {
                task.State = TaskState.Cancelled;
                task.Error = "已取消";
            }
            catch (Exception e)
            {
                task.State = TaskState.Failed;
                task.Error = e.Message;
                Debug.LogError($"[DownloadService] 下载失败: {e}");
            }

            EditorApplication.delayCall += () => OnTasksChanged?.Invoke();
        }

        private static async UniTaskVoid RunInstallAsync(DownloadTask task)
        {
            _isInstalling = true;
            task.State = TaskState.Installing;
            EditorApplication.delayCall += () => OnTasksChanged?.Invoke();

            Debug.Log($"[DownloadService] 开始安装: {task.Dep.displayName ?? task.Dep.id}");

            try
            {
                var manager = new DependencyManager();
                var success = await manager.InstallFromCacheAsync(task.Dep, task.CachePath, task.Cts.Token);

                task.State = success ? TaskState.Completed : TaskState.Failed;
                if (!success)
                {
                    task.Error = "安装失败，缓存可能已损坏";
                    try { if (File.Exists(task.CachePath)) File.Delete(task.CachePath); } catch { }
                }

                if (success)
                {
                    EditorApplication.delayCall += () =>
                    {
                        AssetDatabase.Refresh();
                        OnTasksChanged?.Invoke();
                    };
                }
            }
            catch (Exception e)
            {
                task.State = TaskState.Failed;
                task.Error = "安装失败，缓存可能已损坏";
                try { if (File.Exists(task.CachePath)) File.Delete(task.CachePath); } catch { }
                Debug.LogError($"[DownloadService] 安装失败: {e}");
            }

            _isInstalling = false;
            EditorApplication.delayCall += () => OnTasksChanged?.Invoke();
        }

        private static string GetCachePathForDep(DependencyDefinition dep)
        {
            var ext = dep.source switch
            {
                DependencySource.NuGet => ".nupkg",
                DependencySource.DirectUrl or DependencySource.GitHubRelease => Path.GetExtension(dep.url),
                _ => ".zip"
            };
            if (string.IsNullOrEmpty(ext)) ext = ".zip";
            return DependencyManager.GetCachePath(dep.id, dep.version, ext);
        }

        private static string GetDownloadUrl(DependencyDefinition dep)
        {
            return dep.source switch
            {
                DependencySource.GitHubRepo => GetGitHubRepoUrl(dep.url),
                DependencySource.GitHubRelease => dep.url,
                DependencySource.NuGet => $"https://www.nuget.org/api/v2/package/{dep.id}/{dep.version}",
                DependencySource.DirectUrl => dep.url,
                _ => null
            };
        }

        private static string GetGitHubRepoUrl(string url)
        {
            var branch = "master";
            if (url.Contains("@"))
            {
                var parts = url.Split('@');
                url = parts[0];
                branch = parts[1];
            }
            var segments = url.Split('/');
            return $"https://github.com/{segments[0]}/{segments[1]}/archive/refs/heads/{branch}.zip";
        }

        public static void ClearCompleted()
        {
            var toRemove = new List<string>();
            foreach (var kvp in _tasks)
                if (!kvp.Value.IsRunning)
                    toRemove.Add(kvp.Key);
            foreach (var id in toRemove)
                _tasks.Remove(id);
            OnTasksChanged?.Invoke();
        }
    }
}
#endif
