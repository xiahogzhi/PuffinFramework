#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Puffin.Editor.Hub.Data;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace Puffin.Editor.Hub.Services
{
    /// <summary>
    /// 模块验证结果
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid;
        public List<string> Errors = new();
        public List<string> Warnings = new();
        public HubModuleManifest Manifest;
    }

    /// <summary>
    /// 模块发布器
    /// </summary>
    public class ModulePublisher
    {
        /// <summary>
        /// 验证模块结构
        /// </summary>
        public ValidationResult ValidateModule(string modulePath)
        {
            var result = new ValidationResult { IsValid = true };

            if (!Directory.Exists(modulePath))
            {
                result.Errors.Add($"模块目录不存在: {modulePath}");
                result.IsValid = false;
                return result;
            }

            // 检查 module.json
            var moduleJsonPath = Path.Combine(modulePath, HubConstants.ManifestFileName);
            if (!File.Exists(moduleJsonPath))
            {
                result.Errors.Add($"缺少 {HubConstants.ManifestFileName} 文件");
                result.IsValid = false;
            }
            else
            {
                try
                {
                    result.Manifest = ManifestService.Load(moduleJsonPath);

                    if (string.IsNullOrEmpty(result.Manifest?.moduleId))
                    {
                        result.Errors.Add($"{HubConstants.ManifestFileName} 缺少 moduleId");
                        result.IsValid = false;
                    }
                    if (string.IsNullOrEmpty(result.Manifest?.version))
                    {
                        result.Errors.Add($"{HubConstants.ManifestFileName} 缺少 version");
                        result.IsValid = false;
                    }
                }
                catch (Exception e)
                {
                    result.Errors.Add($"{HubConstants.ManifestFileName} 解析失败: {e.Message}");
                    result.IsValid = false;
                }
            }

            // 检查 Runtime 目录
            var runtimePath = Path.Combine(modulePath, "Runtime");
            if (!Directory.Exists(runtimePath))
                result.Warnings.Add("缺少 Runtime 目录");

            // 检查 asmdef
            var asmdefFiles = Directory.GetFiles(modulePath, "*.asmdef", SearchOption.AllDirectories);
            if (asmdefFiles.Length == 0)
                result.Warnings.Add("缺少 .asmdef 文件");

            // 检查依赖是否都已上传到远程
            if (result.Manifest != null)
            {
                var deps = result.Manifest.moduleDependencies ?? new List<ModuleDependency>();
                var localDeps = new List<string>();
                foreach (var dep in deps)
                {
                    if (dep.optional) continue; // 可选依赖不强制检查
                    var isRemote = IsModuleInRemoteRegistry(dep.moduleId);
                    if (!isRemote)
                        localDeps.Add(dep.moduleId);
                }
                if (localDeps.Count > 0)
                {
                    result.Errors.Add($"以下依赖模块尚未上传到远程仓库，请先上传它们: {string.Join(", ", localDeps)}");
                    result.IsValid = false;
                }
            }

            return result;
        }

        /// <summary>
        /// 检查模块是否存在于任何远程仓库
        /// </summary>
        private bool IsModuleInRemoteRegistry(string moduleId)
        {
            // 检查锁定文件中是否有远程来源
            var lockInfo = InstalledModulesLock.Instance.GetModule(moduleId);
            if (lockInfo != null && !string.IsNullOrEmpty(lockInfo.registryId))
                return true;

            // 检查本地目录是否存在（本地模块）
            var localPath = ManifestService.GetModulePath(moduleId);
            if (!Directory.Exists(localPath))
                return false; // 模块不存在

            // 存在但没有远程来源 = 本地模块
            return false;
        }

        /// <summary>
        /// 打包模块
        /// </summary>
        public async UniTask<string> PackageModuleAsync(string modulePath, string outputDir = null, HubModuleManifest manifestOverride = null)
        {
            var validation = ValidateModule(modulePath);
            if (!validation.IsValid)
            {
                foreach (var error in validation.Errors)
                    Debug.LogError($"[Hub] {error}");
                return null;
            }

            var manifest = manifestOverride ?? validation.Manifest;
            var moduleId = manifest.moduleId;
            var version = manifest.version;

            outputDir ??= Path.Combine(Application.dataPath, "../Library/PuffinHubPackages");
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            var packagePath = Path.Combine(outputDir, $"{moduleId}-{version}.zip");

            try
            {
                // 删除旧包
                if (File.Exists(packagePath))
                    File.Delete(packagePath);

                // 创建 ZIP
                ZipFile.CreateFromDirectory(modulePath, packagePath, CompressionLevel.Optimal, false);

                // 计算校验和
                var checksum = ComputeChecksum(packagePath);
                manifest.checksum = $"sha256:{checksum}";
                manifest.size = new FileInfo(packagePath).Length;
                manifest.downloadUrl = $"{moduleId}-{version}.zip";

                // 生成 manifest.json
                var manifestPath = Path.Combine(outputDir, $"{moduleId}-{version}-manifest.json");
                var manifestJson = JsonUtility.ToJson(manifest, true);
                File.WriteAllText(manifestPath, manifestJson);

                Debug.Log($"[Hub] 打包完成: {packagePath}");
                Debug.Log($"[Hub] 清单文件: {manifestPath}");

                await UniTask.Yield();
                return packagePath;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hub] 打包失败: {e}");
                return null;
            }
        }

        /// <summary>
        /// 生成发布说明模板
        /// </summary>
        public string GenerateReleaseNotes(string modulePath)
        {
            var validation = ValidateModule(modulePath);
            if (!validation.IsValid || validation.Manifest == null)
                return "";

            var manifest = validation.Manifest;
            return $@"# {manifest.displayName ?? manifest.moduleId} v{manifest.version}

## 更新内容
-

## 安装要求
- Unity {manifest.unityVersion ?? "2021.3+"}
- Puffin Framework {manifest.puffinVersion ?? "1.0.0+"}

## 依赖
{(manifest.moduleDependencies?.Count > 0 ? string.Join("\n", manifest.moduleDependencies.Select(d => $"- {d.moduleId}")) : "无")}
";
        }

        /// <summary>
        /// 上传模块到 GitHub 仓库（使用 Contents API）
        /// </summary>
        public async UniTask<bool> UploadToGitHubAsync(string packagePath, HubModuleManifest manifest, RegistrySource registry, Action<string> onStatus = null)
        {
            if (string.IsNullOrEmpty(registry.authToken))
            {
                Debug.LogError("[Hub] 需要配置 GitHub Token 才能上传");
                return false;
            }

            var moduleId = manifest.moduleId;
            var version = manifest.version;

            // 解析 owner/repo
            var (owner, repo) = ParseGitHubUrl(registry.url);
            if (owner == null)
            {
                Debug.LogError($"[Hub] 无效的仓库 URL: {registry.url}，格式应为 owner/repo");
                return false;
            }

            Debug.Log($"[Hub] 上传目标: {owner}/{repo}");

            // 验证仓库
            onStatus?.Invoke("验证仓库...");
            var repoExists = await VerifyRepoAsync(owner, repo, registry.authToken);
            if (!repoExists)
            {
                Debug.LogError($"[Hub] 仓库 {owner}/{repo} 不存在或无权访问（需要 Token 有 repo 权限）");
                return false;
            }

            try
            {
                // 1. 上传 zip 文件
                var zipFileName = $"{moduleId}-{version}.zip";
                var zipPath = $"modules/{moduleId}/{zipFileName}";
                onStatus?.Invoke($"正在上传 {zipFileName}...");
                var zipBytes = File.ReadAllBytes(packagePath);
                var zipSuccess = await UploadFileViaContentsApiAsync(owner, repo, zipPath, zipBytes, $"Upload {zipFileName}", registry.authToken);
                if (!zipSuccess) return false;

                // 2. 上传 manifest.json
                var manifestFilePath = Path.ChangeExtension(packagePath, null) + "-manifest.json";
                var manifestRepoPath = $"modules/{moduleId}/{version}/manifest.json";
                onStatus?.Invoke("正在上传 manifest.json...");
                var manifestBytes = File.ReadAllBytes(manifestFilePath);
                var manifestSuccess = await UploadFileViaContentsApiAsync(owner, repo, manifestRepoPath, manifestBytes, $"Upload manifest for {moduleId}@{version}", registry.authToken);
                if (!manifestSuccess) return false;

                // 3. 更新 registry.json
                onStatus?.Invoke("正在更新 registry.json...");
                var registrySuccess = await UpdateRegistryJsonViaContentsApiAsync(owner, repo, moduleId, version, manifest.displayName, registry.authToken);
                if (!registrySuccess)
                    Debug.LogWarning("[Hub] registry.json 更新失败，模块已上传但可能不会显示在列表中");

                onStatus?.Invoke("上传完成!");
                Debug.Log($"[Hub] 模块 {moduleId}@{version} 已上传到 {registry.name}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hub] 上传失败: {e.Message}");
                return false;
            }
        }

        private (string owner, string repo) ParseGitHubUrl(string url)
        {
            url = url.Trim().TrimEnd('/');
            if (url.StartsWith("https://github.com/"))
                url = url.Substring("https://github.com/".Length);
            if (url.StartsWith("github.com/"))
                url = url.Substring("github.com/".Length);

            var parts = url.Split('/');
            if (parts.Length < 2)
                return (null, null);
            return (parts[0], parts[1]);
        }

        /// <summary>
        /// 通过 Contents API 上传文件
        /// </summary>
        private async UniTask<bool> UploadFileViaContentsApiAsync(string owner, string repo, string path, byte[] content, string message, string token)
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/contents/{path}";

            // 先获取文件 SHA（如果存在）
            string sha = null;
            using (var getRequest = UnityEngine.Networking.UnityWebRequest.Get(url))
            {
                getRequest.SetRequestHeader("Authorization", $"Bearer {token}");
                getRequest.SetRequestHeader("User-Agent", "PuffinHub");
                getRequest.SetRequestHeader("Accept", "application/vnd.github+json");
                getRequest.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

                await getRequest.SendWebRequest();

                if (getRequest.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var json = JObject.Parse(getRequest.downloadHandler.text);
                    sha = json["sha"]?.Value<string>();
                }
            }

            // 构建请求体
            var requestBody = new GitHubContentRequest
            {
                message = message,
                content = Convert.ToBase64String(content),
                branch = "main",
                sha = sha
            };
            var bodyJson = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

            var request = new UnityEngine.Networking.UnityWebRequest(url, "PUT");
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.timeout = 300;
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.SetRequestHeader("User-Agent", "PuffinHub");
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

            await request.SendWebRequest();

            var responseCode = request.responseCode;
            var responseText = request.downloadHandler?.text ?? "";
            request.Dispose();

            if (responseCode == 200 || responseCode == 201)
                return true;

            Debug.LogError($"[Hub] 上传文件 {path} 失败: {responseCode} - {responseText}");
            return false;
        }

        /// <summary>
        /// 通过 Contents API 更新 registry.json
        /// </summary>
        private async UniTask<bool> UpdateRegistryJsonViaContentsApiAsync(string owner, string repo, string moduleId, string version, string displayName, string token)
        {
            const string registryPath = "registry.json";
            var url = $"https://api.github.com/repos/{owner}/{repo}/contents/{registryPath}";

            // 获取现有 registry.json
            string existingContent = null;
            string sha = null;
            using (var getRequest = UnityEngine.Networking.UnityWebRequest.Get(url))
            {
                getRequest.SetRequestHeader("Authorization", $"Bearer {token}");
                getRequest.SetRequestHeader("User-Agent", "PuffinHub");
                getRequest.SetRequestHeader("Accept", "application/vnd.github+json");
                getRequest.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

                await getRequest.SendWebRequest();

                if (getRequest.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var json = JObject.Parse(getRequest.downloadHandler.text);
                    sha = json["sha"]?.Value<string>();
                    var contentBase64 = json["content"]?.Value<string>();
                    if (!string.IsNullOrEmpty(contentBase64))
                        existingContent = Encoding.UTF8.GetString(Convert.FromBase64String(contentBase64.Replace("\n", "")));
                }
            }

            // 构建新的 registry.json
            var registry = new RegistryJson { name = "Puffin Modules", version = "1.0.0", modules = new List<RegistryModuleEntry>() };

            if (!string.IsNullOrEmpty(existingContent))
            {
                try
                {
                    var json = JObject.Parse(existingContent);
                    registry.name = json["name"]?.Value<string>() ?? registry.name;
                    registry.version = json["version"]?.Value<string>() ?? registry.version;

                    var modules = json["modules"] as JObject;
                    if (modules != null)
                    {
                        foreach (var kvp in modules)
                        {
                            var entryId = kvp.Key;
                            var moduleData = kvp.Value as JObject;
                            if (moduleData == null) continue;
                            registry.modules.Add(new RegistryModuleEntry
                            {
                                id = entryId,
                                latest = moduleData["latest"]?.Value<string>(),
                                versions = moduleData["versions"]?.ToObject<List<string>>() ?? new List<string>(),
                                updatedAt = moduleData["updatedAt"]?.Value<string>(),
                            });
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Hub] 解析 registry.json 失败: {e.Message}");
                }
            }

            // 添加/更新模块
            var now = DateTime.UtcNow.ToString("o");
            var existing = registry.modules.Find(m => m.id == moduleId);
            if (existing != null)
            {
                if (!existing.versions.Contains(version))
                    existing.versions.Add(version);
                existing.latest = GetLatestVersion(existing.versions);
                existing.updatedAt = now;
            }
            else
            {
                registry.modules.Add(new RegistryModuleEntry
                {
                    id = moduleId,
                    latest = version,
                    versions = new List<string> { version },
                    updatedAt = now,
                });
            }

            // 生成 JSON 并上传
            var newContent = BuildRegistryJson(registry);
            var contentBytes = Encoding.UTF8.GetBytes(newContent);

            var requestBody = new GitHubContentRequest
            {
                message = $"Update registry.json for {moduleId}@{version}",
                content = Convert.ToBase64String(contentBytes),
                branch = "main",
                sha = sha
            };
            var bodyJson = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

            var request = new UnityEngine.Networking.UnityWebRequest(url, "PUT");
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.SetRequestHeader("User-Agent", "PuffinHub");
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

            await request.SendWebRequest();

            var responseCode = request.responseCode;
            request.Dispose();

            return responseCode == 200 || responseCode == 201;
        }

        private async UniTask<bool> VerifyRepoAsync(string owner, string repo, string token)
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}";
            using var request = UnityEngine.Networking.UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.SetRequestHeader("User-Agent", "PuffinHub");
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

            await request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                return false;

            // 检查写入权限
            var response = request.downloadHandler.text;
            return response.Contains("\"push\":true") || response.Contains("\"admin\":true");
        }

        private string GetLatestVersion(List<string> versions)
        {
            return versions.OrderByDescending(v =>
            {
                var parts = v.Split('.');
                var major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : 0;
                var minor = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
                var patch = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 0;
                return major * 10000 + minor * 100 + patch;
            }).FirstOrDefault() ?? versions.LastOrDefault();
        }

        /// <summary>
        /// 删除远程模块版本（使用 Contents API）
        /// </summary>
        public async UniTask<bool> DeleteVersionAsync(RegistrySource registry, string moduleId, string version, Action<string> onStatus = null)
        {
            if (string.IsNullOrEmpty(registry.authToken))
            {
                Debug.LogError("[Hub] 需要配置 GitHub Token 才能删除");
                return false;
            }

            var (owner, repo) = ParseGitHubUrl(registry.url);
            if (owner == null) return false;

            try
            {
                // 1. 删除 zip 文件
                var zipPath = $"modules/{moduleId}/{moduleId}-{version}.zip";
                onStatus?.Invoke($"正在删除 {moduleId}@{version}...");
                await DeleteFileViaContentsApiAsync(owner, repo, zipPath, $"Delete {moduleId}-{version}.zip", registry.authToken);

                // 2. 删除 manifest.json
                var manifestPath = $"modules/{moduleId}/{version}/manifest.json";
                await DeleteFileViaContentsApiAsync(owner, repo, manifestPath, $"Delete manifest for {moduleId}@{version}", registry.authToken);

                // 3. 更新 registry.json
                onStatus?.Invoke("正在更新 registry.json...");
                await RemoveVersionFromRegistryViaContentsApiAsync(owner, repo, moduleId, version, registry.authToken);

                onStatus?.Invoke("删除完成!");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hub] 删除失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 通过 Contents API 删除文件
        /// </summary>
        private async UniTask<bool> DeleteFileViaContentsApiAsync(string owner, string repo, string path, string message, string token)
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/contents/{path}";

            // 获取文件 SHA
            string sha = null;
            using (var getRequest = UnityEngine.Networking.UnityWebRequest.Get(url))
            {
                getRequest.SetRequestHeader("Authorization", $"Bearer {token}");
                getRequest.SetRequestHeader("User-Agent", "PuffinHub");
                getRequest.SetRequestHeader("Accept", "application/vnd.github+json");
                getRequest.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

                await getRequest.SendWebRequest();

                if (getRequest.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                    return false; // 文件不存在

                var json = JObject.Parse(getRequest.downloadHandler.text);
                sha = json["sha"]?.Value<string>();
            }

            if (string.IsNullOrEmpty(sha)) return false;

            // 删除文件
            var deleteBody = new { message, sha, branch = "main" };
            var bodyJson = JsonConvert.SerializeObject(deleteBody);
            var bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

            var request = new UnityEngine.Networking.UnityWebRequest(url, "DELETE");
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.SetRequestHeader("User-Agent", "PuffinHub");
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

            await request.SendWebRequest();

            var success = request.responseCode == 200;
            request.Dispose();
            return success;
        }

        /// <summary>
        /// 通过 Contents API 从 registry.json 中移除版本
        /// </summary>
        private async UniTask<bool> RemoveVersionFromRegistryViaContentsApiAsync(string owner, string repo, string moduleId, string version, string token)
        {
            const string registryPath = "registry.json";
            var url = $"https://api.github.com/repos/{owner}/{repo}/contents/{registryPath}";

            // 获取现有 registry.json
            string existingContent = null;
            string sha = null;
            using (var getRequest = UnityEngine.Networking.UnityWebRequest.Get(url))
            {
                getRequest.SetRequestHeader("Authorization", $"Bearer {token}");
                getRequest.SetRequestHeader("User-Agent", "PuffinHub");
                getRequest.SetRequestHeader("Accept", "application/vnd.github+json");
                getRequest.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

                await getRequest.SendWebRequest();

                if (getRequest.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                    return false;

                var json = JObject.Parse(getRequest.downloadHandler.text);
                sha = json["sha"]?.Value<string>();
                var contentBase64 = json["content"]?.Value<string>();
                if (!string.IsNullOrEmpty(contentBase64))
                    existingContent = Encoding.UTF8.GetString(Convert.FromBase64String(contentBase64.Replace("\n", "")));
            }

            if (string.IsNullOrEmpty(existingContent)) return false;

            // 解析并修改
            var registry = ParseRegistryJson(existingContent);
            var entry = registry.modules.Find(m => m.id == moduleId);
            if (entry != null)
            {
                entry.versions.Remove(version);
                if (entry.versions.Count == 0)
                    registry.modules.Remove(entry);
                else
                    entry.latest = GetLatestVersion(entry.versions);
            }

            // 生成 JSON 并上传
            var newContent = BuildRegistryJson(registry);
            var contentBytes = Encoding.UTF8.GetBytes(newContent);

            var requestBody = new GitHubContentRequest
            {
                message = $"Remove {moduleId}@{version} from registry",
                content = Convert.ToBase64String(contentBytes),
                branch = "main",
                sha = sha
            };
            var bodyJson = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

            var request = new UnityEngine.Networking.UnityWebRequest(url, "PUT");
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.SetRequestHeader("User-Agent", "PuffinHub");
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

            await request.SendWebRequest();

            var success = request.responseCode == 200 || request.responseCode == 201;
            request.Dispose();
            return success;
        }

        private RegistryJson ParseRegistryJson(string content)
        {
            var registry = new RegistryJson { name = "Puffin Modules", version = "1.0.0", modules = new List<RegistryModuleEntry>() };
            var json = JObject.Parse(content);
            registry.name = json["name"]?.Value<string>() ?? registry.name;
            registry.version = json["version"]?.Value<string>() ?? registry.version;

            var modules = json["modules"] as JObject;
            if (modules != null)
            {
                foreach (var kvp in modules)
                {
                    var entryId = kvp.Key;
                    var moduleData = kvp.Value as JObject;
                    if (moduleData == null) continue;
                    registry.modules.Add(new RegistryModuleEntry
                    {
                        id = entryId,
                        latest = moduleData["latest"]?.Value<string>(),
                        versions = moduleData["versions"]?.ToObject<List<string>>() ?? new List<string>(),
                        updatedAt = moduleData["updatedAt"]?.Value<string>()
                    });
                }
            }
            return registry;
        }

        private string BuildRegistryJson(RegistryJson registry)
        {
            var root = new JObject
            {
                ["name"] = registry.name,
                ["version"] = registry.version,
                ["modules"] = new JObject()
            };

            var modules = (JObject)root["modules"];
            foreach (var m in registry.modules)
            {
                var moduleObj = new JObject
                {
                    ["latest"] = m.latest,
                    ["versions"] = new JArray(m.versions)
                };
                if (!string.IsNullOrEmpty(m.updatedAt))
                    moduleObj["updatedAt"] = m.updatedAt;
                modules[m.id] = moduleObj;
            }

            return root.ToString(Formatting.Indented);
        }

        [Serializable]
        private class RegistryJson
        {
            public string name;
            public string version;
            public List<RegistryModuleEntry> modules;
        }

        [Serializable]
        private class RegistryModuleEntry
        {
            public string id;
            public string latest;
            public List<string> versions;
            public string updatedAt;  // 最后更新时间
        }

        private string ComputeChecksum(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        [Serializable]
        private class GitHubContentRequest
        {
            public string message;
            public string content;
            public string branch;
            public string sha;
        }
    }
}
#endif
