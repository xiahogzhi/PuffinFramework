#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Puffin.Editor.Hub;
using Puffin.Editor.Hub.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace Puffin.Editor.Hub.Services
{
    /// <summary>
    /// 仓库索引服务
    /// </summary>
    public class RegistryService
    {
        private static string ModulesPath => ManifestService.GetModulesPath();
        private readonly Dictionary<string, RegistryIndex> _indexCache = new();
        private readonly Dictionary<string, DateTime> _indexCacheTime = new();
        private readonly Dictionary<string, HubModuleManifest> _manifestCache = new();

        /// <summary>
        /// 获取已安装的模块列表
        /// </summary>
        public List<HubModuleInfo> GetInstalledModules()
        {
            var result = new List<HubModuleInfo>();

            // 扫描模块目录
            if (Directory.Exists(ModulesPath))
            {
                foreach (var dir in Directory.GetDirectories(ModulesPath))
                {
                    var info = CreateModuleInfoFromDir(dir);
                    if (info != null)
                        result.Add(info);
                }
            }

            return result;
        }

        private HubModuleInfo CreateModuleInfoFromDir(string dir)
        {
            var folderName = Path.GetFileName(dir);

            // 验证是否为有效模块
            if (!IsValidModule(dir))
                return null;

            var manifest = ManifestService.Load(ManifestService.GetManifestPathFromDir(dir));

            var moduleId = manifest?.moduleId ?? folderName;
            var version = manifest?.version;
            var displayName = manifest?.displayName ?? moduleId;

            // 从锁定文件获取来源信息
            var lockInfo = InstalledModulesLock.Instance.GetModule(moduleId);
            var sourceRegistryId = lockInfo?.registryId;
            var sourceRegistry = sourceRegistryId != null
                ? HubSettings.Instance.registries.Find(r => r.id == sourceRegistryId)
                : null;

            return new HubModuleInfo
            {
                ModuleId = moduleId,
                DisplayName = displayName,
                Description = manifest?.description,
                Author = manifest?.author,
                Tags = manifest?.tags,
                ReleaseNotes = manifest?.releaseNotes,
                Dependencies = manifest?.moduleDependencies,
                LatestVersion = version,
                InstalledVersion = version,
                RegistryId = sourceRegistryId ?? "local",
                SourceRegistryId = sourceRegistryId,
                SourceRegistryName = sourceRegistry?.name ?? "本地",
                IsInstalled = true,
                HasUpdate = false,
                Manifest = manifest,
                LoadState = ModuleLoadState.Loaded
            };
        }

        /// <summary>
        /// 验证目录是否为有效模块
        /// </summary>
        private bool IsValidModule(string moduleDir)
        {
            // 1. 必须有 module.json
            if (!File.Exists(ManifestService.GetManifestPathFromDir(moduleDir)))
                return false;

            // 2. 必须有 Runtime 或 Editor 目录
            var hasRuntime = Directory.Exists(Path.Combine(moduleDir, HubConstants.RuntimeFolder));
            var hasEditor = Directory.Exists(Path.Combine(moduleDir, HubConstants.EditorFolder));
            if (!hasRuntime && !hasEditor)
                return false;

            // 3. 必须有程序集定义文件 (.asmdef)
            var asmdefFiles = Directory.GetFiles(moduleDir, $"*{HubConstants.AsmdefExtension}", SearchOption.AllDirectories);
            if (asmdefFiles.Length == 0)
                return false;

            return true;
        }

        /// <summary>
        /// 获取指定仓库的远程模块列表（只返回基本信息，manifest 需要懒加载）
        /// </summary>
        public async UniTask<List<HubModuleInfo>> FetchRegistryModulesAsync(RegistrySource registry, Dictionary<string, HubModuleInfo> installedMap, bool forceRefresh = false)
        {
            var result = new List<HubModuleInfo>();

            try
            {
                var index = await FetchRegistryIndexAsync(registry, forceRefresh);
                if (index?.modules == null) return result;

                foreach (var kvp in index.modules)
                {
                    var moduleId = kvp.Key;
                    var versionInfo = kvp.Value;

                    // 检查是否已安装（优先使用本地信息）
                    installedMap.TryGetValue(moduleId, out var installed);
                    var isInstalled = installed != null;
                    var isFromThisRegistry = isInstalled && installed.SourceRegistryId == registry.id;

                    var info = new HubModuleInfo
                    {
                        ModuleId = moduleId,
                        DisplayName = installed?.DisplayName,  // 已安装的用本地名字，否则等懒加载
                        Description = installed?.Description,
                        Author = installed?.Author,
                        Tags = installed?.Tags,
                        ReleaseNotes = installed?.ReleaseNotes,
                        Dependencies = installed?.Dependencies,
                        Manifest = installed?.Manifest,  // 使用本地 Manifest
                        LatestVersion = versionInfo.latest,
                        RemoteVersion = versionInfo.latest,
                        RegistryId = registry.id,
                        InstalledVersion = installed?.InstalledVersion,
                        IsInstalled = isInstalled,
                        SourceRegistryId = installed?.SourceRegistryId ?? (isFromThisRegistry ? registry.id : null),
                        SourceRegistryName = installed?.SourceRegistryName ?? (isFromThisRegistry ? registry.name : null),
                        Versions = versionInfo.versions ?? new List<string> { versionInfo.latest },
                        UpdatedAt = versionInfo.updatedAt,
                        LoadState = isInstalled ? ModuleLoadState.Loaded : ModuleLoadState.NotLoaded
                    };

                    info.HasUpdate = isInstalled &&
                                     !string.IsNullOrEmpty(installed?.InstalledVersion) &&
                                     !string.IsNullOrEmpty(versionInfo.latest) &&
                                     VersionHelper.Compare(versionInfo.latest, installed.InstalledVersion) > 0;

                    result.Add(info);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Hub] 获取仓库 {registry.name} 失败: {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// 懒加载模块的 manifest 信息（仅用于未安装的远程模块）
        /// </summary>
        public async UniTask<bool> LoadModuleManifestAsync(HubModuleInfo module)
        {
            // 已安装模块 LoadState 已设为 Loaded，会在此返回
            if (module.LoadState == ModuleLoadState.Loading || module.LoadState == ModuleLoadState.Loaded)
                return module.LoadState == ModuleLoadState.Loaded;

            module.LoadState = ModuleLoadState.Loading;

            try
            {
                var registry = HubSettings.Instance.registries.Find(r => r.id == module.RegistryId);
                if (registry == null)
                {
                    module.LoadState = ModuleLoadState.Failed;
                    return false;
                }

                var manifest = await GetManifestAsync(registry, module.ModuleId, module.LatestVersion);
                if (manifest != null)
                {
                    module.DisplayName = manifest.displayName;
                    module.Description = manifest.description;
                    module.Author = manifest.author;
                    module.Tags = manifest.tags;
                    module.ReleaseNotes = manifest.releaseNotes;
                    module.Dependencies = manifest.moduleDependencies;
                    module.Manifest = manifest;
                    module.LoadState = ModuleLoadState.Loaded;
                    return true;
                }

                module.LoadState = ModuleLoadState.Failed;
                return false;
            }
            catch
            {
                module.LoadState = ModuleLoadState.Failed;
                return false;
            }
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

            // 添加没有在远程仓库中找到的已安装模块
            foreach (var local in installedModules)
            {
                if (!result.Exists(m => m.ModuleId == local.ModuleId))
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

            var manifest = JsonConvert.DeserializeObject<HubModuleManifest>(json);
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
            var json = JObject.Parse(response);
            var content = json["content"]?.Value<string>();
            if (string.IsNullOrEmpty(content)) return null;

            var base64Content = content.Replace("\n", "");
            var bytes = Convert.FromBase64String(base64Content);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        private Dictionary<string, ModuleVersionInfo> ParseModulesDict(string jsonStr)
        {
            var result = new Dictionary<string, ModuleVersionInfo>();
            var root = JObject.Parse(jsonStr);
            var modules = root["modules"] as JObject;
            if (modules == null) return result;

            foreach (var kvp in modules)
            {
                var moduleId = kvp.Key;
                var moduleData = kvp.Value as JObject;
                if (moduleData == null) continue;

                result[moduleId] = new ModuleVersionInfo
                {
                    latest = moduleData["latest"]?.Value<string>(),
                    versions = moduleData["versions"]?.ToObject<List<string>>() ?? new List<string>(),
                    updatedAt = moduleData["updatedAt"]?.Value<string>()
                };
            }

            return result;
        }

    }
}
#endif
