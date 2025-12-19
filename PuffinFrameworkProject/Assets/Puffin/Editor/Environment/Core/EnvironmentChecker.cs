#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;

namespace Puffin.Editor.Environment.Core
{
    /// <summary>
    /// 环境检查工具，用于检测文件、目录和命令行工具的可用性
    /// </summary>
    public static class EnvironmentChecker
    {
        public static bool FileExists(string path) => File.Exists(path);

        public static bool DirectoryExists(string path) => Directory.Exists(path);

        public static bool HasFiles(string dir, string pattern, SearchOption option = SearchOption.TopDirectoryOnly)
        {
            return DirectoryExists(dir) && Directory.GetFiles(dir, pattern, option).Length > 0;
        }

        public static bool IsInPath(string exeName) => FindInPath(exeName) != null;

        public static string FindInPath(string exeName)
        {
            if (RunCommand(exeName, "--version", out _) || RunCommand(exeName, "", out _))
                return exeName;
            return null;
        }

        public static bool RunCommand(string fileName, string args, out string output, string workDir = null)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    WorkingDirectory = workDir ?? "",
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
