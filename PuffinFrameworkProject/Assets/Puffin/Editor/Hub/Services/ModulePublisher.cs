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
        /// 上传模块到 GitHub 仓库（使用 Releases API，支持最大 2GB 文件）
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
            var releaseTag = $"{moduleId}-{version}";

            // 解析 owner/repo
            var (owner, repo) = ParseGitHubUrl(registry.url);
            if (owner == null)
            {
                Debug.LogError($"[Hub] 无效的仓库 URL: {registry.url}，格式应为 owner/repo");
                return false;
            }

            Debug.Log($"[Hub] 上传目标: {owner}/{repo}, Release Tag: {releaseTag}");

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
                // 1. 创建或获取 Release
                onStatus?.Invoke($"正在创建 Release {releaseTag}...");
                var releaseInfo = await GetOrCreateReleaseAsync(owner, repo, releaseTag, registry.authToken);
                if (releaseInfo == null) return false;

                var (releaseId, uploadUrl) = releaseInfo.Value;

                // 2. 上传 zip 文件
                onStatus?.Invoke($"正在上传 {moduleId}-{version}.zip...");
                var zipBytes = File.ReadAllBytes(packagePath);
                var zipSuccess = await UploadReleaseAssetAsync(owner, repo, releaseId, uploadUrl, $"{moduleId}-{version}.zip", zipBytes, registry.authToken);
                if (!zipSuccess) return false;

                // 3. 上传 manifest.json
                onStatus?.Invoke("正在上传 manifest.json...");
                var manifestPath = Path.ChangeExtension(packagePath, null) + "-manifest.json";
                var manifestBytes = File.ReadAllBytes(manifestPath);
                var manifestSuccess = await UploadReleaseAssetAsync(owner, repo, releaseId, uploadUrl, "manifest.json", manifestBytes, registry.authToken);
                if (!manifestSuccess) return false;

                // 4. 更新 registry.json（存储在 registry Release 中）
                onStatus?.Invoke("正在更新 registry.json...");
                var registrySuccess = await UpdateRegistryJsonAsync(owner, repo, moduleId, version, manifest.displayName, registry.authToken);
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
        /// 获取或创建 Release
        /// </summary>
        private async UniTask<(long releaseId, string uploadUrl)?> GetOrCreateReleaseAsync(string owner, string repo, string tag, string token)
        {
            // 先尝试获取已存在的 Release
            var getUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{tag}";
            try
            {
                using (var getRequest = UnityEngine.Networking.UnityWebRequest.Get(getUrl))
                {
                    getRequest.SetRequestHeader("Authorization", $"Bearer {token}");
                    getRequest.SetRequestHeader("User-Agent", "PuffinHub");
                    getRequest.SetRequestHeader("Accept", "application/vnd.github+json");
                    getRequest.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

                    await getRequest.SendWebRequest();

                    if (getRequest.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        var json = JObject.Parse(getRequest.downloadHandler.text);
                        var id = json["id"]?.Value<long>() ?? 0;
                        var uploadUrl = json["upload_url"]?.Value<string>();
                        if (id > 0 && !string.IsNullOrEmpty(uploadUrl))
                            return (id, uploadUrl);
                    }
                    // 404 是正常的，表示 Release 不存在，继续创建
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Hub] 获取 Release 失败: {e.Message}，尝试创建新 Release");
            }

            // 创建新 Release
            var createUrl = $"https://api.github.com/repos/{owner}/{repo}/releases";
            var body = JsonConvert.SerializeObject(new { tag_name = tag, name = tag, draft = false, prerelease = false });
            var bodyBytes = Encoding.UTF8.GetBytes(body);

            var request = new UnityEngine.Networking.UnityWebRequest(createUrl, "POST");
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.SetRequestHeader("User-Agent", "PuffinHub");
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

            await request.SendWebRequest();

            var responseCode = request.responseCode;
            var responseText = request.downloadHandler?.text ?? "";
            request.Dispose();

            if (responseCode == 201)
            {
                var json = JObject.Parse(responseText);
                var id = json["id"]?.Value<long>() ?? 0;
                var uploadUrl = json["upload_url"]?.Value<string>();
                if (id > 0 && !string.IsNullOrEmpty(uploadUrl))
                    return (id, uploadUrl);
            }

            // 提供更详细的错误信息
            var errorMsg = $"[Hub] 创建 Release 失败: {responseCode}";
            if (responseCode == 404)
                errorMsg += "\n  可能原因: 仓库不存在或 Token 无权访问";
            else if (responseCode == 403)
                errorMsg += "\n  可能原因: Token 权限不足，需要 'contents: write' 权限";
            else if (responseCode == 422)
                errorMsg += "\n  可能原因: Release tag 已存在或参数无效";
            errorMsg += $"\n  响应: {responseText}";
            Debug.LogError(errorMsg);
            return null;
        }

        /// <summary>
        /// 上传 Release Asset（支持覆盖，带重试）
        /// </summary>
        private async UniTask<bool> UploadReleaseAssetAsync(string owner, string repo, long releaseId, string uploadUrl, string fileName, byte[] content, string token)
        {
            // 先删除同名 Asset（如果存在）
            await DeleteReleaseAssetAsync(owner, repo, releaseId, fileName, token);

            // 上传新 Asset（带重试）
            var url = uploadUrl.Replace("{?name,label}", $"?name={Uri.EscapeDataString(fileName)}");
            const int maxRetries = 3;

            for (int retry = 0; retry < maxRetries; retry++)
            {
                if (retry > 0)
                {
                    Debug.Log($"[Hub] 重试上传 {fileName} ({retry}/{maxRetries})...");
                    await UniTask.Delay(1000 * retry); // 递增延迟
                }

                try
                {
                    var request = new UnityEngine.Networking.UnityWebRequest(url, "POST");
                    request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(content);
                    request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                    request.timeout = 300; // 5分钟超时
                    request.SetRequestHeader("Authorization", $"Bearer {token}");
                    request.SetRequestHeader("User-Agent", "PuffinHub");
                    request.SetRequestHeader("Accept", "application/vnd.github+json");
                    request.SetRequestHeader("Content-Type", "application/octet-stream");
                    request.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

                    await request.SendWebRequest();

                    var responseCode = request.responseCode;
                    var responseText = request.downloadHandler?.text ?? "";
                    var error = request.error;
                    request.Dispose();

                    if (responseCode == 201)
                        return true;

                    // 如果是网络错误，重试
                    if (!string.IsNullOrEmpty(error) && (error.Contains("Curl") || error.Contains("HTTP/2")))
                    {
                        Debug.LogWarning($"[Hub] 网络错误: {error}");
                        continue;
                    }

                    Debug.LogError($"[Hub] 上传 Asset {fileName} 失败: {responseCode} - {responseText}");
                    return false;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Hub] 上传异常: {e.Message}");
                    if (retry == maxRetries - 1)
                    {
                        Debug.LogError($"[Hub] 上传 {fileName} 失败，已重试 {maxRetries} 次");
                        return false;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 删除 Release Asset
        /// </summary>
        private async UniTask DeleteReleaseAssetAsync(string owner, string repo, long releaseId, string fileName, string token)
        {
            // 获取 Release 的所有 Assets
            var listUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/{releaseId}/assets";
            using var listRequest = UnityEngine.Networking.UnityWebRequest.Get(listUrl);
            listRequest.SetRequestHeader("Authorization", $"Bearer {token}");
            listRequest.SetRequestHeader("User-Agent", "PuffinHub");
            listRequest.SetRequestHeader("Accept", "application/vnd.github+json");
            listRequest.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

            await listRequest.SendWebRequest();

            if (listRequest.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                return;

            var assets = JArray.Parse(listRequest.downloadHandler.text);
            foreach (var asset in assets)
            {
                if (asset["name"]?.Value<string>() == fileName)
                {
                    var assetId = asset["id"]?.Value<long>() ?? 0;
                    if (assetId > 0)
                    {
                        var deleteUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/assets/{assetId}";
                        var deleteRequest = new UnityEngine.Networking.UnityWebRequest(deleteUrl, "DELETE");
                        deleteRequest.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                        deleteRequest.SetRequestHeader("Authorization", $"Bearer {token}");
                        deleteRequest.SetRequestHeader("User-Agent", "PuffinHub");
                        deleteRequest.SetRequestHeader("Accept", "application/vnd.github+json");
                        deleteRequest.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

                        await deleteRequest.SendWebRequest();
                        deleteRequest.Dispose();
                    }
                    break;
                }
            }
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

        /// <summary>
        /// 更新 registry.json（存储在 registry Release 中）
        /// </summary>
        private async UniTask<bool> UpdateRegistryJsonAsync(string owner, string repo, string moduleId, string version, string displayName, string token)
        {
            const string registryTag = "registry";

            // 获取或创建 registry Release
            var releaseInfo = await GetOrCreateReleaseAsync(owner, repo, registryTag, token);
            if (releaseInfo == null) return false;

            var (releaseId, uploadUrl) = releaseInfo.Value;

            // 获取现有 registry.json
            var existingContent = await GetRegistryJsonFromReleaseAsync(owner, repo, releaseId, token);

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
            return await UploadReleaseAssetAsync(owner, repo, releaseId, uploadUrl, "registry.json", contentBytes, token);
        }

        /// <summary>
        /// 从 Release 获取 registry.json 内容
        /// </summary>
        private async UniTask<string> GetRegistryJsonFromReleaseAsync(string owner, string repo, long releaseId, string token)
        {
            var listUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/{releaseId}/assets";
            using var listRequest = UnityEngine.Networking.UnityWebRequest.Get(listUrl);
            listRequest.SetRequestHeader("Authorization", $"Bearer {token}");
            listRequest.SetRequestHeader("User-Agent", "PuffinHub");
            listRequest.SetRequestHeader("Accept", "application/vnd.github+json");
            listRequest.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

            await listRequest.SendWebRequest();

            if (listRequest.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                return null;

            var assets = JArray.Parse(listRequest.downloadHandler.text);
            foreach (var asset in assets)
            {
                if (asset["name"]?.Value<string>() == "registry.json")
                {
                    var downloadUrl = asset["browser_download_url"]?.Value<string>();
                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        using var downloadRequest = UnityEngine.Networking.UnityWebRequest.Get(downloadUrl);
                        await downloadRequest.SendWebRequest();
                        if (downloadRequest.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                            return downloadRequest.downloadHandler.text;
                    }
                    break;
                }
            }
            return null;
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
        /// 删除远程模块版本（删除对应的 Release）
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

            var releaseTag = $"{moduleId}-{version}";

            try
            {
                // 1. 删除模块版本的 Release
                onStatus?.Invoke($"正在删除 {moduleId}@{version}...");
                var deleteSuccess = await DeleteReleaseByTagAsync(owner, repo, releaseTag, registry.authToken);
                if (!deleteSuccess)
                {
                    Debug.LogWarning($"[Hub] Release {releaseTag} 不存在或删除失败");
                }

                // 2. 更新 registry.json
                onStatus?.Invoke("正在更新 registry.json...");
                await RemoveVersionFromRegistryAsync(owner, repo, moduleId, version, registry.authToken);

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
        /// 通过 tag 删除 Release
        /// </summary>
        private async UniTask<bool> DeleteReleaseByTagAsync(string owner, string repo, string tag, string token)
        {
            // 获取 Release ID
            var getUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{tag}";
            using var getRequest = UnityEngine.Networking.UnityWebRequest.Get(getUrl);
            getRequest.SetRequestHeader("Authorization", $"Bearer {token}");
            getRequest.SetRequestHeader("User-Agent", "PuffinHub");
            getRequest.SetRequestHeader("Accept", "application/vnd.github+json");
            getRequest.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

            await getRequest.SendWebRequest();

            if (getRequest.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                return false;

            var json = JObject.Parse(getRequest.downloadHandler.text);
            var releaseId = json["id"]?.Value<long>() ?? 0;
            if (releaseId == 0) return false;

            // 删除 Release
            var deleteUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/{releaseId}";
            var deleteRequest = new UnityEngine.Networking.UnityWebRequest(deleteUrl, "DELETE");
            deleteRequest.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            deleteRequest.SetRequestHeader("Authorization", $"Bearer {token}");
            deleteRequest.SetRequestHeader("User-Agent", "PuffinHub");
            deleteRequest.SetRequestHeader("Accept", "application/vnd.github+json");
            deleteRequest.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

            await deleteRequest.SendWebRequest();
            var success = deleteRequest.responseCode == 204;
            deleteRequest.Dispose();

            // 删除对应的 tag
            if (success)
            {
                var tagUrl = $"https://api.github.com/repos/{owner}/{repo}/git/refs/tags/{tag}";
                var tagRequest = new UnityEngine.Networking.UnityWebRequest(tagUrl, "DELETE");
                tagRequest.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                tagRequest.SetRequestHeader("Authorization", $"Bearer {token}");
                tagRequest.SetRequestHeader("User-Agent", "PuffinHub");
                tagRequest.SetRequestHeader("Accept", "application/vnd.github+json");
                tagRequest.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

                await tagRequest.SendWebRequest();
                tagRequest.Dispose();
            }

            return success;
        }

        /// <summary>
        /// 从 registry.json 中移除版本
        /// </summary>
        private async UniTask<bool> RemoveVersionFromRegistryAsync(string owner, string repo, string moduleId, string version, string token)
        {
            const string registryTag = "registry";

            // 获取 registry Release
            var releaseInfo = await GetOrCreateReleaseAsync(owner, repo, registryTag, token);
            if (releaseInfo == null) return false;

            var (releaseId, uploadUrl) = releaseInfo.Value;

            // 获取现有 registry.json
            var existingContent = await GetRegistryJsonFromReleaseAsync(owner, repo, releaseId, token);
            if (string.IsNullOrEmpty(existingContent)) return false;

            // 解析并修改
            var registry = ParseRegistryJson(existingContent);
            if (registry.modules.Count == 0)
            {
                Debug.LogWarning($"[Hub] 解析 registry.json 失败，无法删除版本");
                return false;
            }

            var entry = registry.modules.Find(m => m.id == moduleId);
            if (entry != null)
            {
                entry.versions.Remove(version);
                if (entry.versions.Count == 0)
                    registry.modules.Remove(entry);
                else
                    entry.latest = GetLatestVersion(entry.versions);
            }

            var newContent = BuildRegistryJson(registry);
            var contentBytes = Encoding.UTF8.GetBytes(newContent);
            return await UploadReleaseAssetAsync(owner, repo, releaseId, uploadUrl, "registry.json", contentBytes, token);
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
