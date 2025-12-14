#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Puffin.Editor.Environment.Core
{
    public class Downloader
    {
        public int Timeout { get; set; } = 600;
        public event Action<float, long, long, long> OnProgress;

        public async UniTask<bool> DownloadAsync(string url, string destPath, CancellationToken ct = default)
        {
            var dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using var request = UnityWebRequest.Get(url);
            request.downloadHandler = new DownloadHandlerFile(destPath) { removeFileOnAbort = true };
            request.timeout = Timeout;

            var op = request.SendWebRequest();
            long lastBytes = 0, total = 0;
            var lastTime = DateTime.Now;

            while (!op.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    request.Abort();
                    return false;
                }

                var downloaded = (long)request.downloadedBytes;
                if (total == 0 && request.GetResponseHeader("Content-Length") is string len && long.TryParse(len, out var l))
                    total = l;

                var elapsed = (DateTime.Now - lastTime).TotalSeconds;
                if (elapsed >= 0.5)
                {
                    var speed = (long)((downloaded - lastBytes) / elapsed);
                    lastBytes = downloaded;
                    lastTime = DateTime.Now;
                    OnProgress?.Invoke(total > 0 ? (float)downloaded / total : op.progress, downloaded, total, speed);
                }

                await UniTask.Yield();
            }

            var success = request.result == UnityWebRequest.Result.Success;
            if (!success) Debug.LogError($"[Downloader] {request.error}");
            OnProgress?.Invoke(1f, (long)request.downloadedBytes, (long)request.downloadedBytes, 0);
            return success;
        }
    }
}
#endif
