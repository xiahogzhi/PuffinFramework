#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Cysharp.Threading.Tasks;
using Puffin.Editor.Environment;
using Puffin.Editor.Environment.Core;
using Puffin.Editor.Hub.Data;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Hub.Services
{
    /// <summary>
    /// 模块安装器
    /// </summary>
    public class ModuleInstaller
    {
        private readonly RegistryService _registry;
        private readonly ModuleResolver _resolver;
        private readonly DependencyManager _depManager;

        public event Action<string, float> OnProgress;
        public event Action<string> OnStatusChanged;
        public event Action<float, long, long, long> OnDownloadProgress; // progress, downloaded, total, speed

        public ModuleInstaller(RegistryService registry, ModuleResolver resolver)
        {
            _registry = registry;
            _resolver = resolver;
            _depManager = new DependencyManager();
        }

        /// <summary>
        /// 安装模块（含依赖）
        /// </summary>
        public async UniTask<bool> InstallAsync(string moduleId, string version, string registryId)
        {
            Debug.Log($"[Hub] 开始安装: {moduleId}@{version} from {registryId}");
            try
            {
                OnStatusChanged?.Invoke($"正在解析依赖: {moduleId}");

                // 1. 解析依赖
                var resolveResult = await _resolver.ResolveAsync(moduleId, version, registryId);
                Debug.Log($"[Hub] 解析结果: Success={resolveResult.Success}, Modules={resolveResult.Modules?.Count ?? 0}");

                if (!resolveResult.Success)
                {
                    foreach (var conflict in resolveResult.Conflicts)
                        Debug.LogError($"[Hub] {conflict}");
                    return false;
                }

                foreach (var warning in resolveResult.Warnings)
                    Debug.LogWarning($"[Hub] {warning}");

                // 2. 按顺序安装
                var total = resolveResult.Modules.Count;
                for (var i = 0; i < total; i++)
                {
                    var module = resolveResult.Modules[i];
                    OnProgress?.Invoke(module.ModuleId, (float) i / total);

                    // 检查是否真正已安装（锁定文件 + 目录存在）
                    var modulePath = Path.Combine(Application.dataPath, $"Puffin/Modules/{module.ModuleId}");
                    var isReallyInstalled = InstalledModulesLock.Instance.IsInstalled(module.ModuleId) &&
                                            Directory.Exists(modulePath);

                    if (isReallyInstalled)
                    {
                        Debug.Log($"[Hub] 模块已安装: {module.ModuleId}");
                        continue;
                    }

                    // 如果锁定文件有记录但目录不存在，清理锁定文件
                    if (InstalledModulesLock.Instance.IsInstalled(module.ModuleId) && !Directory.Exists(modulePath))
                    {
                        Debug.LogWarning($"[Hub] 清理无效的安装记录: {module.ModuleId}");
                        InstalledModulesLock.Instance.Remove(module.ModuleId);
                    }

                    var success = await InstallSingleModuleAsync(module);
                    if (!success)
                    {
                        Debug.LogError($"[Hub] 安装失败: {module.ModuleId}");
                        return false;
                    }
                }

                OnProgress?.Invoke(moduleId, 1f);
                OnStatusChanged?.Invoke("安装完成");
                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hub] 安装异常: {e}");
                return false;
            }
        }

        /// <summary>
        /// 卸载模块
        /// </summary>
        public async UniTask<bool> UninstallAsync(string moduleId, bool removeDependents = false)
        {
            try
            {
                var modulePath = Path.Combine(Application.dataPath, $"Puffin/Modules/{moduleId}");
                var moduleLock = InstalledModulesLock.Instance.GetModule(moduleId);

                // 检查模块是否存在（本地或已安装）
                if (moduleLock == null && !Directory.Exists(modulePath))
                {
                    Debug.LogWarning($"[Hub] 模块未安装: {moduleId}");
                    return false;
                }

                // 检查是否有其他模块依赖此模块
                if (!removeDependents)
                {
                    var dependents = FindDependents(moduleId);
                    if (dependents.Count > 0)
                    {
                        Debug.LogWarning($"[Hub] 以下模块依赖 {moduleId}: {string.Join(", ", dependents)}");
                        return false;
                    }
                }

                OnStatusChanged?.Invoke($"正在卸载: {moduleId}");

                // 获取模块的环境依赖（卸载前）
                var envDeps = GetModuleEnvDependencies(modulePath);

                // 删除模块目录
                if (Directory.Exists(modulePath))
                {
                    Directory.Delete(modulePath, true);
                    var metaPath = modulePath + ".meta";
                    if (File.Exists(metaPath))
                        File.Delete(metaPath);
                }

                // 更新锁定文件
                if (moduleLock != null)
                    InstalledModulesLock.Instance.Remove(moduleId);

                // 清理无依赖的环境依赖
                CleanupOrphanedEnvDependencies(envDeps, moduleId);

                OnStatusChanged?.Invoke("卸载完成");
                AssetDatabase.Refresh();
                await UniTask.Yield();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hub] 卸载异常: {e}");
                return false;
            }
        }

        /// <summary>
        /// 获取模块的环境依赖
        /// </summary>
        private List<EnvironmentDependency> GetModuleEnvDependencies(string modulePath)
        {
            var manifestPath = Path.Combine(modulePath, "module.json");
            if (!File.Exists(manifestPath)) return null;

            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonUtility.FromJson<HubModuleManifest>(json);
                return manifest?.envDependencies?.ToList();
            }
            catch { return null; }
        }

        /// <summary>
        /// 清理无其他模块依赖的环境依赖
        /// </summary>
        private void CleanupOrphanedEnvDependencies(List<EnvironmentDependency> envDeps, string excludeModuleId)
        {
            if (envDeps == null || envDeps.Count == 0) return;

            var modulesDir = Path.Combine(Application.dataPath, "Puffin/Modules");
            if (!Directory.Exists(modulesDir)) return;

            foreach (var envDep in envDeps)
            {
                // 标记为保留的依赖不清理
                if (envDep.keepOnUninstall) continue;

                // 检查是否有其他模块也依赖此环境
                var hasOtherDependents = false;
                foreach (var moduleDir in Directory.GetDirectories(modulesDir))
                {
                    var otherModuleId = Path.GetFileName(moduleDir);
                    if (otherModuleId == excludeModuleId) continue;

                    var otherEnvDeps = GetModuleEnvDependencies(moduleDir);
                    if (otherEnvDeps != null && otherEnvDeps.Exists(d => d.id == envDep.id))
                    {
                        hasOtherDependents = true;
                        break;
                    }
                }

                if (!hasOtherDependents)
                {
                    var depDef = ConvertToDepDefinition(envDep);
                    if (_depManager.IsInstalled(depDef))
                    {
                        Debug.Log($"[Hub] 清理环境依赖: {envDep.id}");
                        _depManager.Uninstall(depDef);
                    }
                }
            }
        }

        /// <summary>
        /// 更新模块
        /// </summary>
        public async UniTask<bool> UpdateAsync(string moduleId, string targetVersion = null)
        {
            var moduleLock = InstalledModulesLock.Instance.GetModule(moduleId);
            if (moduleLock == null)
            {
                Debug.LogWarning($"[Hub] 模块未安装: {moduleId}");
                return false;
            }

            // 先卸载再安装
            var success = await UninstallAsync(moduleId, false);
            if (!success) return false;

            return await InstallAsync(moduleId, targetVersion, moduleLock.registryId);
        }

        private async UniTask<bool> InstallSingleModuleAsync(ResolvedModule module)
        {
            OnStatusChanged?.Invoke($"正在安装: {module.ModuleId}@{module.Version}");

            var registry = HubSettings.Instance.registries.Find(r => r.id == module.RegistryId);
            if (registry == null) return false;

            var manifest = module.Manifest;

            // 1. 安装必须的环境依赖（可选依赖跳过）
            if (manifest.envDependencies != null && manifest.envDependencies.Length > 0)
            {
                OnStatusChanged?.Invoke($"正在安装环境依赖: {module.ModuleId}");
                foreach (var envDep in manifest.envDependencies)
                {
                    if (envDep.optional) continue;  // 跳过可选依赖
                    var depDef = ConvertToDepDefinition(envDep);
                    if (!_depManager.IsInstalled(depDef))
                    {
                        var success = await _depManager.InstallAsync(depDef);
                        if (!success)
                        {
                            Debug.LogError($"[Hub] 环境依赖安装失败: {envDep.id}");
                            return false;
                        }
                    }
                }
            }

            // 2. 下载模块包
            var downloadUrl = string.IsNullOrEmpty(manifest.downloadUrl)
                ? registry.GetDownloadUrl(module.ModuleId, module.Version, $"{module.ModuleId}-{module.Version}.zip")
                : manifest.downloadUrl.StartsWith("http")
                    ? manifest.downloadUrl
                    : registry.GetDownloadUrl(module.ModuleId, module.Version, manifest.downloadUrl);

            var cacheDir = HubSettings.CacheDir;
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            var cachePath = Path.Combine(cacheDir, $"{module.ModuleId}-{module.Version}.zip");

            OnStatusChanged?.Invoke($"正在下载: {module.ModuleId}");
            bool downloaded;

            // 使用 GitHub API 下载（绕过 CDN 缓存）
            if (HubSettings.Instance.useGitHubApiDownload && registry.IsGitHubRepo)
            {
                var filePath = $"modules/{module.ModuleId}/{module.Version}/{module.ModuleId}-{module.Version}.zip";
                var apiUrl = registry.GetFileApiUrl(filePath);
                downloaded = await DownloadViaGitHubApiAsync(apiUrl, cachePath, registry.authToken);
            }
            else
            {
                var downloader = new Downloader();
                downloader.OnProgress += (p, dl, total, speed) => OnDownloadProgress?.Invoke(p, dl, total, speed);
                downloaded = await downloader.DownloadAsync(downloadUrl, cachePath);
            }

            if (!downloaded)
            {
                Debug.LogError($"[Hub] 下载失败: {downloadUrl}");
                return false;
            }

            // 3. 验证校验和
            if (!string.IsNullOrEmpty(manifest.checksum))
            {
                if (!VerifyChecksum(cachePath, manifest.checksum, out var actualChecksum))
                {
                    Debug.LogError($"[Hub] 校验和不匹配: {module.ModuleId}\n  期望: {manifest.checksum}\n  实际: sha256:{actualChecksum}");
                    File.Delete(cachePath);
                    return false;
                }
            }

            // 4. 解压到模块目录
            var destPath = Path.Combine(Application.dataPath, $"Puffin/Modules/{module.ModuleId}");
            if (Directory.Exists(destPath))
                Directory.Delete(destPath, true);

            OnStatusChanged?.Invoke($"正在解压: {module.ModuleId}");
            Extractor.Extract(cachePath, destPath);

            // 5. 更新锁定文件
            var lockEntry = new InstalledModuleLock
            {
                moduleId = module.ModuleId,
                version = module.Version,
                registryId = module.RegistryId,
                checksum = manifest.checksum,
                installedAt = DateTime.Now.ToString("o"),
                resolvedDependencies = new List<string>()
            };

            if (manifest.dependencies != null)
            {
                foreach (var dep in manifest.dependencies)
                    lockEntry.resolvedDependencies.Add(dep);
            }

            InstalledModulesLock.Instance.AddOrUpdate(lockEntry);

            // 6. 更新程序集引用
            try
            {
                var deps = manifest.GetAllDependencies();
                AsmdefDependencyResolver.UpdateModuleAsmdefReferences(module.ModuleId, destPath, deps);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Hub] 更新程序集引用失败: {e.Message}");
            }

            Debug.Log($"[Hub] 安装成功: {module.ModuleId}@{module.Version}");
            return true;
        }

        private DependencyDefinition ConvertToDepDefinition(EnvironmentDependency envDep)
        {
            return new DependencyDefinition
            {
                id = envDep.id,
                source = (DependencySource) envDep.source,
                type = (DependencyType) envDep.type,
                url = envDep.url,
                version = envDep.version,
                installDir = envDep.installDir,
                extractPath = envDep.extractPath,
                requiredFiles = envDep.requiredFiles,
                targetFrameworks = envDep.targetFrameworks,
                requirement = envDep.optional ? DependencyRequirement.Optional : DependencyRequirement.Required
            };
        }

        /// <summary>
        /// 通过 GitHub API 下载文件（绕过 CDN 缓存）
        /// </summary>
        private async UniTask<bool> DownloadViaGitHubApiAsync(string apiUrl, string savePath, string authToken)
        {
            if (string.IsNullOrEmpty(apiUrl))
                return false;

            using var request = UnityEngine.Networking.UnityWebRequest.Get(apiUrl);
            request.SetRequestHeader("Accept", "application/vnd.github.v3+json");
            request.SetRequestHeader("User-Agent", "PuffinHub");
            if (!string.IsNullOrEmpty(authToken))
                request.SetRequestHeader("Authorization", $"Bearer {authToken}");

            await request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Hub] GitHub API 下载失败: {request.error}");
                return false;
            }

            // GitHub API 返回 JSON，content 字段是 base64 编码
            var response = request.downloadHandler.text;
            var contentMatch = System.Text.RegularExpressions.Regex.Match(response, "\"content\"\\s*:\\s*\"([^\"]+)\"");
            if (!contentMatch.Success)
            {
                Debug.LogWarning("[Hub] GitHub API 响应中没有 content 字段");
                return false;
            }

            var base64Content = contentMatch.Groups[1].Value.Replace("\\n", "");
            var bytes = Convert.FromBase64String(base64Content);
            File.WriteAllBytes(savePath, bytes);
            return true;
        }

        private bool VerifyChecksum(string filePath, string expectedChecksum, out string actualChecksum)
        {
            actualChecksum = null;
            if (string.IsNullOrEmpty(expectedChecksum))
                return true;

            if (!expectedChecksum.StartsWith("sha256:"))
            {
                Debug.LogWarning($"[Hub] 未知的校验和格式: {expectedChecksum}，跳过验证");
                return true;
            }

            var expected = expectedChecksum.Substring(7).ToLower();
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            actualChecksum = BitConverter.ToString(hash).Replace("-", "").ToLower();

            return actualChecksum == expected;
        }

        /// <summary>
        /// 获取依赖指定模块的所有模块
        /// </summary>
        public List<string> GetDependents(string moduleId) => FindDependents(moduleId);

        private List<string> FindDependents(string moduleId)
        {
            var dependents = new HashSet<string>();

            // 检查锁定文件中的依赖
            foreach (var module in InstalledModulesLock.Instance.modules)
            {
                if (module.moduleId == moduleId) continue;
                if (module.resolvedDependencies != null &&
                    module.resolvedDependencies.Exists(d => d.StartsWith(moduleId + "@") || d == moduleId))
                {
                    dependents.Add(module.moduleId);
                }
            }

            // 检查本地模块的 module.json
            var modulesDir = Path.Combine(Application.dataPath, "Puffin/Modules");
            if (Directory.Exists(modulesDir))
            {
                foreach (var moduleDir in Directory.GetDirectories(modulesDir))
                {
                    var dirName = Path.GetFileName(moduleDir);
                    if (dirName == moduleId) continue;

                    var manifestPath = Path.Combine(moduleDir, "module.json");
                    if (!File.Exists(manifestPath)) continue;

                    try
                    {
                        var json = File.ReadAllText(manifestPath);
                        var manifest = UnityEngine.JsonUtility.FromJson<HubModuleManifest>(json);
                        var deps = manifest?.GetAllDependencies();
                        if (deps != null && deps.Exists(d => d.moduleId == moduleId && !d.optional))
                            dependents.Add(manifest.moduleId ?? dirName);
                    }
                    catch
                    {
                    }
                }
            }

            return dependents.ToList();
        }
    }
}
#endif