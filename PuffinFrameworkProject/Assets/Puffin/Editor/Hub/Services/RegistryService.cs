#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Puffin.Editor.Hub.Data;
using Puffin.Runtime.Tools;
using UnityEngine;
using UnityEngine.Networking;

namespace Puffin.Editor.Hub.Services
{
    /// <summary>
    /// 仓库索引服务
    /// </summary>
    public class RegistryService
    {
        private readonly Dictionary<string, RegistryIndex> _indexCache = new();
        private readonly Dictionary<string, DateTime> _indexCacheTime = new();
        private readonly Dictionary<string, HubModuleManifest> _manifestCache = new();

        /// <summary>
        /// 获取已安装的模块列表
        /// </summary>
        public List<HubModuleInfo> GetInstalledModules()
        {
            var result = new List<HubModuleInfo>();
            var modulesPath = Path.Combine(UnityEngine.Application.dataPath, "Puffin/Modules");

            if (!Directory.Exists(modulesPath))
                return result;

            foreach (var dir in Directory.GetDirectories(modulesPath))
            {
                var folderName = Path.GetFileName(dir);

                // 验证是否为有效模块
                if (!IsValidModule(dir))
                    continue;

                var moduleJsonPath = Path.Combine(dir, "module.json");
                var json = File.ReadAllText(moduleJsonPath);
                var manifest = UnityEngine.JsonUtility.FromJson<HubModuleManifest>(json);

                var moduleId = manifest?.moduleId ?? folderName;
                var version = manifest?.version;
                var displayName = manifest?.displayName ?? moduleId;

                // 从锁定文件获取来源信息
                var lockInfo = InstalledModulesLock.Instance.GetModule(moduleId);
                var sourceRegistryId = lockInfo?.registryId;
                var sourceRegistry = sourceRegistryId != null
                    ? HubSettings.Instance.registries.Find(r => r.id == sourceRegistryId)
                    : null;

                result.Add(new HubModuleInfo
                {
                    ModuleId = moduleId,
                    DisplayName = displayName,
                    LatestVersion = version,
                    InstalledVersion = version,
                    RegistryId = sourceRegistryId ?? "local",
                    SourceRegistryId = sourceRegistryId,
                    SourceRegistryName = sourceRegistry?.name,
                    IsInstalled = true,
                    IsLocal = sourceRegistryId == null,
                    HasUpdate = false,
                    Manifest = manifest
                });
            }

            return result;
        }

        /// <summary>
        /// 验证目录是否为有效模块
        /// </summary>
        private bool IsValidModule(string moduleDir)
        {
            // 1. 必须有 module.json
            var moduleJsonPath = Path.Combine(moduleDir, "module.json");
            if (!File.Exists(moduleJsonPath))
                return false;

            // 2. 必须有 Runtime 或 Editor 目录
            var hasRuntime = Directory.Exists(Path.Combine(moduleDir, "Runtime"));
            var hasEditor = Directory.Exists(Path.Combine(moduleDir, "Editor"));
            if (!hasRuntime && !hasEditor)
                return false;

            // 3. 必须有程序集定义文件 (.asmdef)
            var asmdefFiles = Directory.GetFiles(moduleDir, "*.asmdef", SearchOption.AllDirectories);
            if (asmdefFiles.Length == 0)
                return false;

            return true;
        }

        /// <summary>
        /// 获取指定仓库的远程模块列表
        /// </summary>
        public async UniTask<List<HubModuleInfo>> FetchRegistryModulesAsync(RegistrySource registry, Dictionary<string, HubModuleInfo> installedMap, bool forceRefresh = false)
        {
            var result = new List<HubModuleInfo>();

            try
            {
                var index = await FetchRegistryIndexAsync(registry, forceRefresh);
                if (index?.modules == null) return result;

                // 并行获取所有模块的 manifest
                var manifestTasks = new List<UniTask<(string moduleId, HubModuleManifest manifest)>>();
                foreach (var kvp in index.modules)
                {
                    var moduleId = kvp.Key;
                    var version = kvp.Value.latest;
                    manifestTasks.Add(FetchManifestWithIdAsync(registry, moduleId, version));
                }
                var manifests = await UniTask.WhenAll(manifestTasks);
                var manifestMap = manifests.Where(m => m.manifest != null).ToDictionary(m => m.moduleId, m => m.manifest);

                foreach (var kvp in index.modules)
                {
                    var moduleId = kvp.Key;
                    var versionInfo = kvp.Value;

                    // 检查是否已安装且来源匹配
                    installedMap.TryGetValue(moduleId, out var installed);
                    var isInstalled = installed != null && installed.SourceRegistryId == registry.id;
                    var installedVersion = isInstalled ? installed.InstalledVersion : null;

                    // 从 manifest 获取 displayName
                    manifestMap.TryGetValue(moduleId, out var manifest);
                    var displayName = installed?.DisplayName ?? manifest?.displayName ?? moduleId;

                    var info = new HubModuleInfo
                    {
                        ModuleId = moduleId,
                        DisplayName = displayName,
                        Description = manifest?.description,
                        LatestVersion = versionInfo.latest,
                        RemoteVersion = versionInfo.latest,
                        RegistryId = registry.id,
                        InstalledVersion = installedVersion,
                        IsInstalled = isInstalled,
                        IsLocal = false,
                        HasRemote = true,
                        SourceRegistryId = isInstalled ? registry.id : null,
                        SourceRegistryName = isInstalled ? registry.name : null,
                        Versions = versionInfo.versions ?? new List<string> { versionInfo.latest },
                        UpdatedAt = versionInfo.updatedAt,
                        Manifest = manifest
                    };

                    info.HasUpdate = isInstalled &&
                                     !string.IsNullOrEmpty(installedVersion) &&
                                     !string.IsNullOrEmpty(versionInfo.latest) &&
                                     CompareVersions(versionInfo.latest, installedVersion) > 0;

                    result.Add(info);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Hub] 获取仓库 {registry.name} 失败: {e.Message}");
            }

            return result;
        }

        private async UniTask<(string moduleId, HubModuleManifest manifest)> FetchManifestWithIdAsync(RegistrySource registry, string moduleId, string version)
        {
            var manifest = await GetManifestAsync(registry, moduleId, version);
            return (moduleId, manifest);
        }

        /// <summary>
        /// 获取所有模块列表（用于全部视图）
        /// </summary>
        public async UniTask<List<HubModuleInfo>> FetchAllModulesAsync()
        {
            var result = new List<HubModuleInfo>();
            var installedModules = GetInstalledModules();
            var installedMap = installedModules.ToDictionary(m => m.ModuleId);
            var registries = HubSettings.Instance.GetEnabledRegistries();

            // 获取所有远程模块
            foreach (var registry in registries)
            {
                var remoteModules = await FetchRegistryModulesAsync(registry, installedMap);
                result.AddRange(remoteModules);
            }

            // 添加仅本地的模块（没有远程来源的）
            foreach (var local in installedModules)
            {
                if (local.IsLocal)
                    result.Add(local);
            }

            return result;
        }


        /// <summary>
        /// 获取仓库索引
        /// </summary>
        public async UniTask<RegistryIndex> FetchRegistryIndexAsync(RegistrySource registry, bool forceRefresh = false)
        {
            // 检查缓存
            if (!forceRefresh && _indexCache.TryGetValue(registry.id, out var cached))
            {
                if (_indexCacheTime.TryGetValue(registry.id, out var cacheTime))
                {
                    if ((DateTime.Now - cacheTime).TotalHours < HubSettings.Instance.cacheExpireHours)
                        return cached;
                }
            }

            string json;
            // 强制刷新时使用 GitHub API 绕过 CDN 缓存
            if (forceRefresh && registry.IsGitHubRepo)
            {
                json = await FetchGitHubApiContentAsync(registry.GetRegistryApiUrl(), registry.authToken);
            }
            else
            {
                var url = registry.GetRegistryUrl();
                json = await FetchJsonAsync(url, registry.authToken, forceRefresh);
            }

            if (string.IsNullOrEmpty(json))
                return null;

            var index = JsonUtility.FromJson<RegistryIndex>(json);
            if (index != null)
            {
                index.modules = ParseModulesDict(json);
                _indexCache[registry.id] = index;
                _indexCacheTime[registry.id] = DateTime.Now;
            }

            return index;
        }

        /// <summary>
        /// 获取模块清单
        /// </summary>
        public async UniTask<HubModuleManifest> GetManifestAsync(RegistrySource registry, string moduleId, string version)
        {
            var cacheKey = $"{registry.id}:{moduleId}@{version}";
            if (_manifestCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var url = registry.GetManifestUrl(moduleId, version);
            var json = await FetchJsonAsync(url, registry.authToken);
            if (string.IsNullOrEmpty(json))
                return null;

            var manifest = JsonUtility.FromJson<HubModuleManifest>(json);
            if (manifest != null)
                _manifestCache[cacheKey] = manifest;

            return manifest;
        }

        /// <summary>
        /// 获取模块所有版本
        /// </summary>
        public async UniTask<List<string>> GetVersionsAsync(RegistrySource registry, string moduleId)
        {
            var index = await FetchRegistryIndexAsync(registry);
            if (index?.modules == null)
                return new List<string>();

            if (index.modules.TryGetValue(moduleId, out var info))
                return info.versions ?? new List<string>();

            return new List<string>();
        }

        /// <summary>
        /// 搜索模块
        /// </summary>
        public List<HubModuleInfo> Search(List<HubModuleInfo> modules, string keyword, string[] tags = null)
        {
            var result = modules;

            if (!string.IsNullOrEmpty(keyword))
            {
                keyword = keyword.ToLower();
                result = result.Where(m =>
                    m.ModuleId.ToLower().Contains(keyword) ||
                    (m.DisplayName?.ToLower().Contains(keyword) ?? false) ||
                    (m.Description?.ToLower().Contains(keyword) ?? false)
                ).ToList();
            }

            if (tags != null && tags.Length > 0)
            {
                result = result.Where(m =>
                    m.Tags != null && m.Tags.Any(t => tags.Contains(t))
                ).ToList();
            }

            return result;
        }

        /// <summary>
        /// 刷新所有缓存
        /// </summary>
        public void ClearCache()
        {
            _indexCache.Clear();
            _indexCacheTime.Clear();
            _manifestCache.Clear();
        }

        private async UniTask<string> FetchJsonAsync(string url, string authToken = null, bool noCache = false)
        {
            // 添加时间戳防止缓存
            var finalUrl = noCache ? $"{url}{(url.Contains("?") ? "&" : "?")}t={DateTime.Now.Ticks}" : url;
            using var request = UnityWebRequest.Get(finalUrl);
            if (!string.IsNullOrEmpty(authToken))
                request.SetRequestHeader("Authorization", $"Bearer {authToken}");
            request.SetRequestHeader("Cache-Control", "no-cache");

            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                return null;

            return request.downloadHandler.text;
        }

        /// <summary>
        /// 通过 GitHub API 获取文件内容（绕过 CDN 缓存）
        /// </summary>
        private async UniTask<string> FetchGitHubApiContentAsync(string apiUrl, string authToken = null)
        {
            if (string.IsNullOrEmpty(apiUrl))
                return null;

            using var request = UnityWebRequest.Get(apiUrl);
            request.SetRequestHeader("Accept", "application/vnd.github.v3+json");
            request.SetRequestHeader("User-Agent", "PuffinHub");
            if (!string.IsNullOrEmpty(authToken))
                request.SetRequestHeader("Authorization", $"Bearer {authToken}");

            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Hub] GitHub API 请求失败: {request.error}");
                return null;
            }

            // GitHub API 返回 JSON，content 字段是 base64 编码
            var response = request.downloadHandler.text;
            var json = JsonValue.Parse(response);
            var content = json["content"].AsRawString();
            if (string.IsNullOrEmpty(content)) return null;

            var base64Content = content.Replace("\\n", "");
            var bytes = Convert.FromBase64String(base64Content);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        private Dictionary<string, ModuleVersionInfo> ParseModulesDict(string jsonStr)
        {
            var result = new Dictionary<string, ModuleVersionInfo>();
            var root = JsonValue.Parse(jsonStr);
            var modules = root["modules"];
            if (modules.Type != JsonType.Object) return result;

            var enumerator = modules.GetObjectEnumerator();
            while (enumerator.MoveNext())
            {
                var (moduleId, moduleData) = enumerator.Current;
                result[moduleId] = new ModuleVersionInfo
                {
                    latest = moduleData["latest"].AsString(),
                    versions = moduleData["versions"].ToStringList() ?? new List<string>(),
                    updatedAt = moduleData["updatedAt"].AsString()
                };
            }

            return result;
        }

        private int CompareVersions(string v1, string v2)
        {
            var parts1 = v1.Split('.');
            var parts2 = v2.Split('.');

            for (var i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
            {
                var p1 = i < parts1.Length && int.TryParse(parts1[i], out var n1) ? n1 : 0;
                var p2 = i < parts2.Length && int.TryParse(parts2[i], out var n2) ? n2 : 0;
                if (p1 != p2) return p1.CompareTo(p2);
            }
            return 0;
        }
    }
}
#endif
