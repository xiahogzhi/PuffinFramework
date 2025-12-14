#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
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
                var moduleJsonPath = Path.Combine(dir, "module.json");

                string moduleId = folderName;
                string version = null;
                string displayName = folderName;

                if (File.Exists(moduleJsonPath))
                {
                    try
                    {
                        var json = File.ReadAllText(moduleJsonPath);
                        var manifest = UnityEngine.JsonUtility.FromJson<HubModuleManifest>(json);
                        if (manifest != null)
                        {
                            moduleId = manifest.moduleId ?? folderName;
                            version = manifest.version;
                            displayName = manifest.displayName ?? moduleId;
                        }
                    }
                    catch { }
                }

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
                    HasUpdate = false
                });
            }

            return result;
        }

        /// <summary>
        /// 获取指定仓库的远程模块列表
        /// </summary>
        public async UniTask<List<HubModuleInfo>> FetchRegistryModulesAsync(RegistrySource registry, Dictionary<string, HubModuleInfo> installedMap)
        {
            var result = new List<HubModuleInfo>();

            try
            {
                var index = await FetchRegistryIndexAsync(registry);
                if (index?.modules == null) return result;

                foreach (var kvp in index.modules)
                {
                    var moduleId = kvp.Key;
                    var versionInfo = kvp.Value;

                    // 检查是否已安装且来源匹配
                    installedMap.TryGetValue(moduleId, out var installed);
                    var isInstalled = installed != null && installed.SourceRegistryId == registry.id;
                    var installedVersion = isInstalled ? installed.InstalledVersion : null;

                    var info = new HubModuleInfo
                    {
                        ModuleId = moduleId,
                        DisplayName = installed?.DisplayName ?? moduleId,
                        LatestVersion = versionInfo.latest,
                        RemoteVersion = versionInfo.latest,
                        RegistryId = registry.id,
                        InstalledVersion = installedVersion,
                        IsInstalled = isInstalled,
                        IsLocal = false,
                        HasRemote = true,
                        SourceRegistryId = isInstalled ? registry.id : null,
                        SourceRegistryName = isInstalled ? registry.name : null,
                        Versions = versionInfo.versions ?? new List<string> { versionInfo.latest }
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

            var url = registry.GetRegistryUrl();
            var json = await FetchJsonAsync(url, registry.authToken);
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

        private async UniTask<string> FetchJsonAsync(string url, string authToken = null)
        {
            using var request = UnityWebRequest.Get(url);
            if (!string.IsNullOrEmpty(authToken))
                request.SetRequestHeader("Authorization", $"Bearer {authToken}");

            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                return null;

            return request.downloadHandler.text;
        }

        private Dictionary<string, ModuleVersionInfo> ParseModulesDict(string json)
        {
            var result = new Dictionary<string, ModuleVersionInfo>();

            // 简单解析 "modules": { "ModuleId": { "latest": "x.x.x", "versions": [...] } }
            var modulesStart = json.IndexOf("\"modules\"", StringComparison.Ordinal);
            if (modulesStart < 0) return result;

            var braceStart = json.IndexOf('{', modulesStart + 9);
            if (braceStart < 0) return result;

            var depth = 1;
            var pos = braceStart + 1;
            var currentKey = "";
            var inString = false;
            var stringStart = -1;

            while (pos < json.Length && depth > 0)
            {
                var c = json[pos];

                if (c == '"' && (pos == 0 || json[pos - 1] != '\\'))
                {
                    if (!inString)
                    {
                        inString = true;
                        stringStart = pos + 1;
                    }
                    else
                    {
                        inString = false;
                        if (depth == 1 && string.IsNullOrEmpty(currentKey))
                            currentKey = json.Substring(stringStart, pos - stringStart);
                    }
                }
                else if (!inString)
                {
                    if (c == '{')
                    {
                        if (depth == 1 && !string.IsNullOrEmpty(currentKey))
                        {
                            var objStart = pos;
                            var objDepth = 1;
                            pos++;
                            while (pos < json.Length && objDepth > 0)
                            {
                                if (json[pos] == '{') objDepth++;
                                else if (json[pos] == '}') objDepth--;
                                pos++;
                            }
                            var objJson = json.Substring(objStart, pos - objStart);
                            var versionInfo = JsonUtility.FromJson<ModuleVersionInfo>(objJson);
                            if (versionInfo != null)
                                result[currentKey] = versionInfo;
                            currentKey = "";
                            continue;
                        }
                        depth++;
                    }
                    else if (c == '}')
                    {
                        depth--;
                    }
                }
                pos++;
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
