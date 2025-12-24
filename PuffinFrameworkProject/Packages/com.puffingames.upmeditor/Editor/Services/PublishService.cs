#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PuffinGames.UPMEditor
{
    /// <summary>
    /// Service for packing and publishing UPM packages
    /// </summary>
    public static class PublishService
    {
        private const string PrefsRegistryKey = "UPMEditor_Registry";
        private const string DefaultRegistry = "http://localhost:4873";

        /// <summary>
        /// Get saved registry URL
        /// </summary>
        public static string GetRegistry()
        {
            return EditorPrefs.GetString(PrefsRegistryKey, DefaultRegistry);
        }

        /// <summary>
        /// Save registry URL
        /// </summary>
        public static void SetRegistry(string registry)
        {
            EditorPrefs.SetString(PrefsRegistryKey, registry);
        }

        /// <summary>
        /// Pack package to tgz file using npm pack
        /// </summary>
        public static PackResult Pack(string packagePath, string outputDirectory = null)
        {
            var result = new PackResult { PackagePath = packagePath };

            var npmPath = FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                result.Success = false;
                result.ErrorMessage = "npm not found. Please install Node.js and restart Unity.";
                return result;
            }

            var fullPackagePath = Path.GetFullPath(packagePath);
            if (!Directory.Exists(fullPackagePath))
            {
                result.Success = false;
                result.ErrorMessage = $"Package directory not found: {fullPackagePath}";
                return result;
            }

            // Default output to package directory
            var outputDir = string.IsNullOrEmpty(outputDirectory)
                ? fullPackagePath
                : Path.GetFullPath(outputDirectory);

            try
            {
                // Run npm pack
                var args = "pack";
                var (exitCode, stdout, stderr) = RunNpmCommand(npmPath, args, fullPackagePath);

                if (exitCode == 0)
                {
                    // npm pack outputs the filename
                    var tgzFileName = stdout.Trim();
                    var tgzPath = Path.Combine(fullPackagePath, tgzFileName);

                    // Move to output directory if different
                    if (outputDir != fullPackagePath && File.Exists(tgzPath))
                    {
                        var targetPath = Path.Combine(outputDir, tgzFileName);
                        if (!Directory.Exists(outputDir))
                            Directory.CreateDirectory(outputDir);
                        if (File.Exists(targetPath))
                            File.Delete(targetPath);
                        File.Move(tgzPath, targetPath);
                        tgzPath = targetPath;
                    }

                    result.Success = true;
                    result.TgzPath = tgzPath;
                    Debug.Log($"<color=green>Package packed successfully:</color> {tgzPath}");
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = !string.IsNullOrEmpty(stderr) ? stderr : $"npm pack failed with code {exitCode}";
                    Debug.LogError($"Pack failed: {result.ErrorMessage}");
                }
            }
            catch (Exception e)
            {
                result.Success = false;
                result.ErrorMessage = e.Message;
                Debug.LogError($"Pack error: {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// Publish package to registry using npm publish
        /// </summary>
        public static PublishResult Publish(string packagePath, string registry = null)
        {
            var result = new PublishResult { PackagePath = packagePath };

            var npmPath = FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                result.Success = false;
                result.ErrorMessage = "npm not found. Please install Node.js and restart Unity.";
                return result;
            }

            var fullPackagePath = Path.GetFullPath(packagePath);
            if (!Directory.Exists(fullPackagePath))
            {
                result.Success = false;
                result.ErrorMessage = $"Package directory not found: {fullPackagePath}";
                return result;
            }

            registry = registry ?? GetRegistry();

            try
            {
                var args = $"publish --registry {registry}";
                Debug.Log($"[npm] {args}");

                var (exitCode, stdout, stderr) = RunNpmCommand(npmPath, args, fullPackagePath);

                if (!string.IsNullOrEmpty(stdout))
                    Debug.Log(stdout);

                if (exitCode == 0)
                {
                    result.Success = true;
                    Debug.Log($"<color=green>Package published successfully to {registry}</color>");
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = !string.IsNullOrEmpty(stderr) ? stderr : $"npm publish failed with code {exitCode}";
                    Debug.LogError($"Publish failed: {result.ErrorMessage}");
                }
            }
            catch (Exception e)
            {
                result.Success = false;
                result.ErrorMessage = e.Message;
                Debug.LogError($"Publish error: {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// Publish a .tgz file to registry using npm publish
        /// </summary>
        public static PublishResult PublishTgz(string tgzPath, string registry = null)
        {
            var result = new PublishResult { PackagePath = tgzPath };

            var npmPath = FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                result.Success = false;
                result.ErrorMessage = "npm not found. Please install Node.js and restart Unity.";
                return result;
            }

            if (!File.Exists(tgzPath))
            {
                result.Success = false;
                result.ErrorMessage = $"tgz file not found: {tgzPath}";
                return result;
            }

            registry = registry ?? GetRegistry();

            try
            {
                var args = $"publish \"{tgzPath}\" --registry {registry}";
                Debug.Log($"[npm] {args}");

                var (exitCode, stdout, stderr) = RunNpmCommand(npmPath, args, Path.GetDirectoryName(tgzPath));

                if (!string.IsNullOrEmpty(stdout))
                    Debug.Log(stdout);

                if (exitCode == 0)
                {
                    result.Success = true;
                    Debug.Log($"<color=green>Package published successfully to {registry}</color>");
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = !string.IsNullOrEmpty(stderr) ? stderr : $"npm publish failed with code {exitCode}";
                    Debug.LogError($"Publish failed: {result.ErrorMessage}");
                }
            }
            catch (Exception e)
            {
                result.Success = false;
                result.ErrorMessage = e.Message;
                Debug.LogError($"Publish error: {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// Unpublish package version from registry
        /// </summary>
        public static PublishResult Unpublish(string packageName, string version, string registry = null)
        {
            var result = new PublishResult();

            var npmPath = FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                result.Success = false;
                result.ErrorMessage = "npm not found. Please install Node.js and restart Unity.";
                return result;
            }

            registry = registry ?? GetRegistry();

            try
            {
                var args = $"unpublish {packageName}@{version} --registry {registry}";
                Debug.Log($"[npm] {args}");

                var (exitCode, stdout, stderr) = RunNpmCommand(npmPath, args, Directory.GetCurrentDirectory());

                if (!string.IsNullOrEmpty(stdout))
                    Debug.Log(stdout);

                if (exitCode == 0)
                {
                    result.Success = true;
                    Debug.Log($"<color=green>Package {packageName}@{version} unpublished from {registry}</color>");
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = !string.IsNullOrEmpty(stderr) ? stderr : $"npm unpublish failed with code {exitCode}";
                    Debug.LogError($"Unpublish failed: {result.ErrorMessage}");
                }
            }
            catch (Exception e)
            {
                result.Success = false;
                result.ErrorMessage = e.Message;
                Debug.LogError($"Unpublish error: {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// Generate scoped registry config for manifest.json
        /// </summary>
        public static string GenerateScopedRegistryConfig(string packageName, string registry = null)
        {
            registry = registry ?? GetRegistry();
            var scope = packageName.Contains(".")
                ? string.Join(".", packageName.Split('.')[0], packageName.Split('.')[1])
                : packageName;

            return $@"{{
  ""scopedRegistries"": [
    {{
      ""name"": ""Private Registry"",
      ""url"": ""{registry}"",
      ""scopes"": [""{scope}""]
    }}
  ]
}}";
        }

        /// <summary>
        /// Check if npm is available
        /// </summary>
        public static bool IsNpmAvailable()
        {
            return !string.IsNullOrEmpty(FindNpmPath());
        }

        #region Unity Signing

        private const string PrefsUnityUsernameKey = "UPMEditor_UnityUsername";
        private const string PrefsCloudOrgIdKey = "UPMEditor_CloudOrgId";

        /// <summary>
        /// Get saved Unity username
        /// </summary>
        public static string GetUnityUsername()
        {
            return EditorPrefs.GetString(PrefsUnityUsernameKey, "");
        }

        /// <summary>
        /// Save Unity username
        /// </summary>
        public static void SetUnityUsername(string username)
        {
            EditorPrefs.SetString(PrefsUnityUsernameKey, username);
        }

        /// <summary>
        /// Get saved Cloud Organization ID
        /// </summary>
        public static string GetCloudOrgId()
        {
            return EditorPrefs.GetString(PrefsCloudOrgIdKey, "");
        }

        /// <summary>
        /// Save Cloud Organization ID
        /// </summary>
        public static void SetCloudOrgId(string orgId)
        {
            EditorPrefs.SetString(PrefsCloudOrgIdKey, orgId);
        }

        /// <summary>
        /// Pack package with Unity signature using -upmPack command
        /// Requires Unity 6.3+
        /// </summary>
        public static PackResult PackWithSignature(string packagePath, string outputDirectory, string username, string password, string cloudOrgId)
        {
            var result = new PackResult { PackagePath = packagePath };

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                result.Success = false;
                result.ErrorMessage = "Unity ID 用户名和密码不能为空";
                return result;
            }

            if (string.IsNullOrEmpty(cloudOrgId))
            {
                result.Success = false;
                result.ErrorMessage = "Cloud Organization ID 不能为空";
                return result;
            }

            var fullPackagePath = Path.GetFullPath(packagePath);
            if (!Directory.Exists(fullPackagePath))
            {
                result.Success = false;
                result.ErrorMessage = $"包目录不存在: {fullPackagePath}";
                return result;
            }

            var outputDir = string.IsNullOrEmpty(outputDirectory)
                ? fullPackagePath
                : Path.GetFullPath(outputDirectory);

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            var unityPath = EditorApplication.applicationPath;
            var projectPath = Path.GetDirectoryName(Application.dataPath);

            Debug.Log($"[Unity Sign] Unity 路径: {unityPath}");
            Debug.Log($"[Unity Sign] 项目路径: {projectPath}");
            Debug.Log($"[Unity Sign] 包路径: {fullPackagePath}");
            Debug.Log($"[Unity Sign] 输出路径: {outputDir}");

            try
            {
                // 添加 -projectPath 和 -logFile 参数
                var logFile = Path.Combine(outputDir, "upm_pack_log.txt");
                var args = $"-batchmode -projectPath \"{projectPath}\" " +
                           $"-username \"{username}\" -password \"{password}\" " +
                           $"-upmPack \"{fullPackagePath}\" \"{outputDir}\" " +
                           $"-cloudOrganization \"{cloudOrgId}\" " +
                           $"-logFile \"{logFile}\"";

                Debug.Log($"[Unity Sign] 正在执行签名打包...");

                var (exitCode, stdout, stderr) = RunCommand(unityPath, args);

                Debug.Log($"[Unity Sign] 退出码: {exitCode}");
                if (!string.IsNullOrEmpty(stdout))
                    Debug.Log($"[Unity Sign] stdout: {stdout}");
                if (!string.IsNullOrEmpty(stderr))
                    Debug.Log($"[Unity Sign] stderr: {stderr}");

                // 列出输出目录中的文件
                Debug.Log($"[Unity Sign] 输出目录内容:");
                foreach (var file in Directory.GetFiles(outputDir))
                {
                    Debug.Log($"  - {Path.GetFileName(file)}");
                }

                // 读取日志文件
                if (File.Exists(logFile))
                {
                    var logContent = File.ReadAllText(logFile);
                    if (logContent.Length > 2000)
                        logContent = logContent.Substring(logContent.Length - 2000);
                    Debug.Log($"[Unity Sign] 日志文件内容 (最后2000字符):\n{logContent}");
                }

                // Find the generated tgz file
                var packageData = PackageJsonService.ReadPackageJson(packagePath);
                var expectedTgz = $"{packageData.name}-{packageData.version}.tgz";
                var tgzPath = Path.Combine(outputDir, expectedTgz);

                if (File.Exists(tgzPath))
                {
                    result.Success = true;
                    result.TgzPath = tgzPath;
                    Debug.Log($"<color=green>签名包已生成:</color> {tgzPath}");
                }
                else
                {
                    // Try to find any tgz file
                    var tgzFiles = Directory.GetFiles(outputDir, "*.tgz");
                    if (tgzFiles.Length > 0)
                    {
                        result.Success = true;
                        result.TgzPath = tgzFiles[0];
                        Debug.Log($"<color=green>签名包已生成:</color> {result.TgzPath}");
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorMessage = $"打包完成但未找到 .tgz 文件\n退出码: {exitCode}\n{stderr}";
                    }
                }
            }
            catch (Exception e)
            {
                result.Success = false;
                result.ErrorMessage = e.Message;
                Debug.LogError($"签名打包错误: {e.Message}");
            }

            return result;
        }

        private static (int exitCode, string stdout, string stderr) RunCommand(string fileName, string args)
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

            return (process.ExitCode, stdout, stderr);
        }

        #endregion

        private static (int exitCode, string stdout, string stderr) RunNpmCommand(string npmPath, string args, string workDir)
        {
            var psi = new ProcessStartInfo
            {
                FileName = npmPath,
                Arguments = args,
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, stdout, stderr);
        }

        private static string FindNpmPath()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "npm.cmd"),
                @"C:\Program Files\nodejs\npm.cmd",
                "/usr/local/bin/npm",
                "/usr/bin/npm",
                "/opt/homebrew/bin/npm"
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }

            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var p in pathEnv.Split(Path.PathSeparator))
            {
                var npm = Path.Combine(p, Application.platform == RuntimePlatform.WindowsEditor ? "npm.cmd" : "npm");
                if (File.Exists(npm)) return npm;
            }

            return null;
        }
    }

    /// <summary>
    /// Result of pack operation
    /// </summary>
    public class PackResult
    {
        public bool Success;
        public string PackagePath;
        public string TgzPath;
        public string ErrorMessage;
    }

    /// <summary>
    /// Result of publish operation
    /// </summary>
    public class PublishResult
    {
        public bool Success;
        public string PackagePath;
        public string ErrorMessage;
    }
}
#endif
