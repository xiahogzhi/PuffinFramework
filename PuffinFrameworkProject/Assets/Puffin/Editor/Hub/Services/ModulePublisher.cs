#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using Cysharp.Threading.Tasks;
using Puffin.Editor.Hub.Data;
using Puffin.Runtime.Tools;
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
            var moduleJsonPath = Path.Combine(modulePath, "module.json");
            if (!File.Exists(moduleJsonPath))
            {
                result.Errors.Add("缺少 module.json 文件");
                result.IsValid = false;
            }
            else
            {
                try
                {
                    var json = File.ReadAllText(moduleJsonPath);
                    result.Manifest = JsonUtility.FromJson<HubModuleManifest>(json);

                    if (string.IsNullOrEmpty(result.Manifest.moduleId))
                    {
                        result.Errors.Add("module.json 缺少 moduleId");
                        result.IsValid = false;
                    }
                    if (string.IsNullOrEmpty(result.Manifest.version))
                    {
                        result.Errors.Add("module.json 缺少 version");
                        result.IsValid = false;
                    }
                }
                catch (Exception e)
                {
                    result.Errors.Add($"module.json 解析失败: {e.Message}");
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
                var deps = result.Manifest.GetAllDependencies();
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
            var localPath = Path.Combine(Application.dataPath, $"Puffin/Modules/{moduleId}");
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
{(manifest.dependencies?.Count > 0 ? string.Join("\n", manifest.dependencies.Select(d => $"- {d}")) : "无")}
";
        }

        /// <summary>
        /// 上传模块到 GitHub 仓库
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
            var basePath = $"modules/{moduleId}/{version}";

            // 解析 owner/repo
            var url = registry.url.Trim().TrimEnd('/');
            // 移除可能的 https://github.com/ 前缀
            if (url.StartsWith("https://github.com/"))
                url = url.Substring("https://github.com/".Length);
            if (url.StartsWith("github.com/"))
                url = url.Substring("github.com/".Length);

            var parts = url.Split('/');
            if (parts.Length < 2)
            {
                Debug.LogError($"[Hub] 无效的仓库 URL: {registry.url}，格式应为 owner/repo");
                return false;
            }
            var owner = parts[0];
            var repo = parts[1];

            // 验证仓库和分支
            onStatus?.Invoke("验证仓库...");
            var repoExists = await VerifyRepoAsync(owner, repo, registry.authToken);
            if (!repoExists)
            {
                Debug.LogError($"[Hub] 仓库 {owner}/{repo} 不存在或无权访问");
                return false;
            }

            onStatus?.Invoke("验证分支...");
            var branchExists = await VerifyBranchAsync(owner, repo, registry.branch, registry.authToken);
            if (!branchExists)
            {
                Debug.LogError($"[Hub] 分支 '{registry.branch}' 不存在");
                return false;
            }

            try
            {
                // 1. 上传 zip 文件
                onStatus?.Invoke($"正在上传 {moduleId}-{version}.zip...");
                var zipBytes = File.ReadAllBytes(packagePath);
                var zipSuccess = await UploadFileAsync(owner, repo, registry.branch, $"{basePath}/{moduleId}-{version}.zip", zipBytes, registry.authToken);
                if (!zipSuccess) return false;

                // 2. 上传 manifest.json
                onStatus?.Invoke("正在上传 manifest.json...");
                var manifestPath = Path.ChangeExtension(packagePath, null) + "-manifest.json";
                var manifestBytes = File.ReadAllBytes(manifestPath);
                var manifestSuccess = await UploadFileAsync(owner, repo, registry.branch, $"{basePath}/manifest.json", manifestBytes, registry.authToken);
                if (!manifestSuccess) return false;

                // 3. 更新 registry.json
                onStatus?.Invoke("正在更新 registry.json...");
                var registrySuccess = await UpdateRegistryJsonAsync(owner, repo, registry.branch, moduleId, version, manifest.displayName, registry.authToken);
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

        private async UniTask<bool> UploadFileAsync(string owner, string repo, string branch, string path, byte[] content, string token)
        {
            var encodedPath = string.Join("/", path.Split('/').Select(Uri.EscapeDataString));
            var url = $"https://api.github.com/repos/{owner}/{repo}/contents/{encodedPath}";

            var base64Content = Convert.ToBase64String(content);

            // GitHub Contents API 限制 100MB
            if (content.Length > 100 * 1024 * 1024)
            {
                Debug.LogError($"[Hub] 文件太大 ({content.Length / 1024.0 / 1024.0:F2} MB)，超过 GitHub Contents API 100MB 限制");
                return false;
            }

            // 检查文件是否存在（获取 sha 用于更新）
            string sha = null;
            try
            {
                var getRequest = UnityEngine.Networking.UnityWebRequest.Get(url);
                getRequest.SetRequestHeader("Authorization", $"Bearer {token}");
                getRequest.SetRequestHeader("User-Agent", "PuffinHub");
                getRequest.SetRequestHeader("Accept", "application/vnd.github+json");
                getRequest.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

                await getRequest.SendWebRequest();

                if (getRequest.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var json = JsonValue.Parse(getRequest.downloadHandler.text);
                    sha = json["sha"].AsString();
                }
                getRequest.Dispose();
            }
            catch { /* 文件不存在，正常 */ }

            // 构建请求体
            string body;
            if (!string.IsNullOrEmpty(sha))
                body = $"{{\"message\":\"Update {Path.GetFileName(path)}\",\"content\":\"{base64Content}\",\"branch\":\"{branch}\",\"sha\":\"{sha}\"}}";
            else
                body = $"{{\"message\":\"Add {Path.GetFileName(path)}\",\"content\":\"{base64Content}\",\"branch\":\"{branch}\"}}";

            var bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);
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
            var responseText = request.downloadHandler?.text ?? "";
            var result = request.result;
            request.Dispose();

            if (responseCode == 201 || responseCode == 200)
                return true;

            Debug.LogError($"[Hub] 上传 {Path.GetFileName(path)} 失败: {responseCode} - {responseText}");
            return false;
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

        private async UniTask<bool> VerifyBranchAsync(string owner, string repo, string branch, string token)
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/branches/{branch}";
            using var request = UnityEngine.Networking.UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.SetRequestHeader("User-Agent", "PuffinHub");
            request.SetRequestHeader("Accept", "application/vnd.github+json");

            await request.SendWebRequest();

            return request.result == UnityEngine.Networking.UnityWebRequest.Result.Success;
        }

        /// <summary>
        /// 更新 registry.json，添加新模块版本
        /// </summary>
        private async UniTask<bool> UpdateRegistryJsonAsync(string owner, string repo, string branch, string moduleId, string version, string displayName, string token)
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/contents/registry.json?ref={branch}";

            // 获取现有 registry.json
            string existingContent = null;
            string sha = null;

            try
            {
                var getRequest = UnityEngine.Networking.UnityWebRequest.Get(url);
                getRequest.SetRequestHeader("Authorization", $"Bearer {token}");
                getRequest.SetRequestHeader("User-Agent", "PuffinHub");
                getRequest.SetRequestHeader("Accept", "application/vnd.github+json");
                getRequest.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");

                await getRequest.SendWebRequest();

                if (getRequest.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var json = JsonValue.Parse(getRequest.downloadHandler.text);
                    sha = json["sha"].AsString();
                    var content = json["content"].AsRawString();
                    if (!string.IsNullOrEmpty(content))
                    {
                        var base64 = content.Replace("\\n", "");
                        existingContent = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                    }
                }
                getRequest.Dispose();
            }
            catch { /* registry.json 不存在 */ }

            // 构建新的 registry.json
            var registry = new RegistryJson { name = "Puffin Modules", version = "1.0.0", modules = new List<RegistryModuleEntry>() };

            if (!string.IsNullOrEmpty(existingContent))
            {
                try
                {
                    var json = JsonValue.Parse(existingContent);
                    registry.name = json["name"].AsString() ?? registry.name;
                    registry.version = json["version"].AsString() ?? registry.version;

                    var modules = json["modules"];
                    if (modules.Type == JsonType.Object)
                    {
                        var enumerator = modules.GetObjectEnumerator();
                        while (enumerator.MoveNext())
                        {
                            var (entryId, moduleData) = enumerator.Current;
                            registry.modules.Add(new RegistryModuleEntry
                            {
                                id = entryId,
                                latest = moduleData["latest"].AsString(),
                                versions = moduleData["versions"].ToStringList() ?? new List<string>(),
                                updatedAt = moduleData["updatedAt"].AsString(),
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

            // 生成 JSON
            var newContent = BuildRegistryJson(registry);

            // 上传
            var contentBytes = System.Text.Encoding.UTF8.GetBytes(newContent);
            return await UploadFileAsync(owner, repo, branch, "registry.json", contentBytes, token);
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
        /// 删除远程模块版本
        /// </summary>
        public async UniTask<bool> DeleteVersionAsync(RegistrySource registry, string moduleId, string version, Action<string> onStatus = null)
        {
            if (string.IsNullOrEmpty(registry.authToken))
            {
                Debug.LogError("[Hub] 需要配置 GitHub Token 才能删除");
                return false;
            }

            var url = registry.url.Trim().TrimEnd('/');
            if (url.StartsWith("https://github.com/")) url = url.Substring("https://github.com/".Length);
            if (url.StartsWith("github.com/")) url = url.Substring("github.com/".Length);
            var parts = url.Split('/');
            if (parts.Length < 2) return false;
            var owner = parts[0];
            var repo = parts[1];

            try
            {
                // 1. 删除版本目录下的文件
                onStatus?.Invoke($"正在删除 {moduleId}@{version}...");
                var basePath = $"modules/{moduleId}/{version}";

                // 获取目录内容
                var filesUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{basePath}?ref={registry.branch}";
                var filesJson = await FetchGitHubApiAsync(filesUrl, registry.authToken);
                if (!string.IsNullOrEmpty(filesJson))
                {
                    // 解析文件列表并删除（GitHub API 返回数组）
                    var filesArray = JsonValue.Parse(filesJson);
                    if (filesArray.Type == JsonType.Array)
                    {
                        var enumerator = filesArray.GetArrayEnumerator();
                        while (enumerator.MoveNext())
                        {
                            var file = enumerator.Current;
                            var filePath = file["path"].AsString();
                            var sha = file["sha"].AsString();
                            if (!string.IsNullOrEmpty(filePath) && !string.IsNullOrEmpty(sha))
                                await DeleteFileAsync(owner, repo, registry.branch, filePath, sha, registry.authToken);
                        }
                    }
                }

                // 2. 更新 registry.json
                onStatus?.Invoke("正在更新 registry.json...");
                await RemoveVersionFromRegistryAsync(owner, repo, registry.branch, moduleId, version, registry.authToken);

                onStatus?.Invoke("删除完成!");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hub] 删除失败: {e.Message}");
                return false;
            }
        }

        private async UniTask<string> FetchGitHubApiAsync(string url, string token)
        {
            using var request = UnityEngine.Networking.UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.SetRequestHeader("User-Agent", "PuffinHub");
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            await request.SendWebRequest();
            return request.result == UnityEngine.Networking.UnityWebRequest.Result.Success ? request.downloadHandler.text : null;
        }

        private async UniTask<bool> DeleteFileAsync(string owner, string repo, string branch, string path, string sha, string token)
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/contents/{path}";
            var body = $"{{\"message\":\"Delete {System.IO.Path.GetFileName(path)}\",\"sha\":\"{sha}\",\"branch\":\"{branch}\"}}";
            var bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);

            var request = new UnityEngine.Networking.UnityWebRequest(url, "DELETE");
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.SetRequestHeader("User-Agent", "PuffinHub");
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("Content-Type", "application/json");

            await request.SendWebRequest();
            var success = request.responseCode == 200;
            request.Dispose();
            return success;
        }

        private async UniTask<bool> RemoveVersionFromRegistryAsync(string owner, string repo, string branch, string moduleId, string version, string token)
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/contents/registry.json?ref={branch}";
            var response = await FetchGitHubApiAsync(url, token);
            if (string.IsNullOrEmpty(response)) return false;

            var json = JsonValue.Parse(response);
            var sha = json["sha"].AsString();
            var content = json["content"].AsRawString();
            if (string.IsNullOrEmpty(sha) || string.IsNullOrEmpty(content)) return false;

            var base64 = content.Replace("\\n", "");
            var existingContent = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));

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
            var contentBytes = System.Text.Encoding.UTF8.GetBytes(newContent);
            return await UploadFileAsync(owner, repo, branch, "registry.json", contentBytes, token);
        }

        private RegistryJson ParseRegistryJson(string content)
        {
            var registry = new RegistryJson { name = "Puffin Modules", version = "1.0.0", modules = new List<RegistryModuleEntry>() };
            var json = JsonValue.Parse(content);
            registry.name = json["name"].AsString() ?? registry.name;
            registry.version = json["version"].AsString() ?? registry.version;

            var modules = json["modules"];
            if (modules.Type == JsonType.Object)
            {
                var enumerator = modules.GetObjectEnumerator();
                while (enumerator.MoveNext())
                {
                    var (entryId, moduleData) = enumerator.Current;
                    registry.modules.Add(new RegistryModuleEntry
                    {
                        id = entryId,
                        latest = moduleData["latest"].AsString(),
                        versions = moduleData["versions"].ToStringList() ?? new List<string>(),
                        updatedAt = moduleData["updatedAt"].AsString()
                    });
                }
            }
            return registry;
        }

        private string BuildRegistryJson(RegistryJson registry)
        {
            var builder = new JsonBuilder();
            builder.BeginObject();
            builder.Property("name", registry.name);
            builder.Property("version", registry.version);
            builder.Key("modules").BeginObject();
            foreach (var m in registry.modules)
            {
                builder.Key(m.id).BeginObject();
                builder.Property("latest", m.latest);
                builder.StringArray("versions", m.versions);
                builder.PropertyIf("updatedAt", m.updatedAt);
                builder.EndObject();
            }
            builder.EndObject();
            builder.EndObject();
            return builder.ToString();
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
