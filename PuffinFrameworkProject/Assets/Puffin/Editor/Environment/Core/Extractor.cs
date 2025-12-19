#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Puffin.Editor.Environment.Core
{
    /// <summary>
    /// 压缩包解压工具，支持 ZIP 和 7z 格式
    /// </summary>
    public static class Extractor
    {
        private static readonly string[] SevenZipPaths =
        {
            Path.Combine(Application.dataPath, "../Tools/7z/7zr.exe"),
            "7z", "7za", "7zr",
            @"C:\Program Files\7-Zip\7z.exe",
            @"C:\Program Files (x86)\7-Zip\7z.exe"
        };

        public static bool ExtractZip(string archivePath, string destDir)
        {
            try
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
                Directory.CreateDirectory(destDir);
                ZipFile.ExtractToDirectory(archivePath, destDir);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Extractor] ExtractZip 失败: {e.Message}\n路径: {archivePath}");
                return false;
            }
        }

        public static bool Extract7z(string archivePath, string destDir)
        {
            foreach (var exe in SevenZipPaths)
            {
                if (RunCommand(exe, $"x \"{archivePath}\" -o\"{destDir}\" -y", out _))
                    return true;
            }
            return false;
        }

        public static bool Extract(string archivePath, string destDir)
        {
            // 根据文件头判断格式
            var format = DetectFormat(archivePath);
            return format switch
            {
                "zip" => ExtractZip(archivePath, destDir),
                "7z" => Extract7z(archivePath, destDir),
                _ => false
            };
        }

        private static string DetectFormat(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                var header = new byte[6];
                fs.Read(header, 0, 6);
                // ZIP: PK (0x50 0x4B)
                if (header[0] == 0x50 && header[1] == 0x4B) return "zip";
                // 7z: 7z (0x37 0x7A 0xBC 0xAF 0x27 0x1C)
                if (header[0] == 0x37 && header[1] == 0x7A && header[2] == 0xBC) return "7z";
                return Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            }
            catch { return ""; }
        }

        public static bool Is7zAvailable()
        {
            foreach (var exe in SevenZipPaths)
            {
                if (RunCommand(exe, "", out _))
                    return true;
            }
            return false;
        }

        public static async UniTask<bool> Install7zAsync(Downloader downloader)
        {
            var destDir = Path.Combine(Application.dataPath, "../Tools/7z");
            Directory.CreateDirectory(destDir);
            var exePath = Path.Combine(destDir, "7zr.exe");
            return await downloader.DownloadAsync("https://www.7-zip.org/a/7zr.exe", exePath);
        }

        private static bool RunCommand(string fileName, string args, out string output)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                output = string.IsNullOrEmpty(stderr) ? stdout : $"{stdout}\n{stderr}";
                return process.ExitCode == 0;
            }
            catch
            {
                output = "";
                return false;
            }
        }
    }
}
#endif
