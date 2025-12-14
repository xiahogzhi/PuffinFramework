#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
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
            try
            {
                OnStatusChanged?.Invoke($"正在解析依赖: {moduleId}");

                // 1. 解析依赖
                var resolveResult = await _resolver.ResolveAsync(moduleId, version, registryId);
                if (!resolveResult.Success)
                {
                    foreach (var conflict in resolveResult.Conflicts)
                        Debug.LogError($"[Hub] {conflict}");
                    return false;
                }

                // 2. 按顺序安装
                var total = resolveResult.Modules.Count;
                for (var i = 0; i < total; i++)
                {
                    var module = resolveResult.Modules[i];
                    OnProgress?.Invoke(module.ModuleId, (float)i / total);

                    if (InstalledModulesLock.Instance.IsInstalled(module.ModuleId))
                    {
                        Debug.Log($"[Hub] 模块已安装: {module.ModuleId}");
                        continue;
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
                var moduleLock = InstalledModulesLock.Instance.GetModule(moduleId);
                if (moduleLock == null)
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

                // 删除模块目录
                var modulePath = Path.Combine(Application.dataPath, $"Puffin/Modules/{moduleId}");
                if (Directory.Exists(modulePath))
                {
                    Directory.Delete(modulePath, true);
                    var metaPath = modulePath + ".meta";
                    if (File.Exists(metaPath))
                        File.Delete(metaPath);
                }

                // 更新锁定文件
                InstalledModulesLock.Instance.Remove(moduleId);

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

            // 1. 安装环境依赖
            if (manifest.envDependencies != null && manifest.envDependencies.Length > 0)
            {
                OnStatusChanged?.Invoke($"正在安装环境依赖: {module.ModuleId}");
                foreach (var envDep in manifest.envDependencies)
                {
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
            var downloader = new Downloader();
            var downloaded = await downloader.DownloadAsync(downloadUrl, cachePath);
            if (!downloaded)
            {
                Debug.LogError($"[Hub] 下载失败: {downloadUrl}");
                return false;
            }

            // 3. 验证校验和
            if (!string.IsNullOrEmpty(manifest.checksum))
            {
                if (!VerifyChecksum(cachePath, manifest.checksum))
                {
                    Debug.LogError($"[Hub] 校验和不匹配: {module.ModuleId}");
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
                    lockEntry.resolvedDependencies.Add($"{dep.moduleId}@{dep.versionRange}");
            }

            InstalledModulesLock.Instance.AddOrUpdate(lockEntry);

            Debug.Log($"[Hub] 安装成功: {module.ModuleId}@{module.Version}");
            return true;
        }

        private DependencyDefinition ConvertToDepDefinition(EnvironmentDependency envDep)
        {
            return new DependencyDefinition
            {
                id = envDep.id,
                source = (DependencySource)envDep.source,
                type = (DependencyType)envDep.type,
                url = envDep.url,
                version = envDep.version,
                installDir = envDep.installDir,
                requiredFiles = envDep.requiredFiles
            };
        }

        private bool VerifyChecksum(string filePath, string expectedChecksum)
        {
            if (!expectedChecksum.StartsWith("sha256:"))
                return true;

            var expected = expectedChecksum.Substring(7);
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            var actual = BitConverter.ToString(hash).Replace("-", "").ToLower();
            return actual == expected.ToLower();
        }

        private List<string> FindDependents(string moduleId)
        {
            var dependents = new List<string>();
            foreach (var module in InstalledModulesLock.Instance.modules)
            {
                if (module.resolvedDependencies != null &&
                    module.resolvedDependencies.Exists(d => d.StartsWith(moduleId + "@")))
                {
                    dependents.Add(module.moduleId);
                }
            }
            return dependents;
        }
    }
}
#endif
