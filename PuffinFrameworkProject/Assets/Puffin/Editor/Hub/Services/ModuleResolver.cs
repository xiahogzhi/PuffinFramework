#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Puffin.Editor.Hub.Data;

namespace Puffin.Editor.Hub.Services
{
    /// <summary>
    /// 依赖解析结果
    /// </summary>
    public class ResolveResult
    {
        public bool Success;
        public List<ResolvedModule> Modules = new();
        public List<string> Conflicts = new();
        public List<string> Warnings = new();
    }

    /// <summary>
    /// 已解析的模块
    /// </summary>
    public class ResolvedModule
    {
        public string ModuleId;
        public string Version;
        public string RegistryId;
        public HubModuleManifest Manifest;
    }

    /// <summary>
    /// 模块更新信息
    /// </summary>
    public class ModuleUpdate
    {
        public string ModuleId;
        public string CurrentVersion;
        public string LatestVersion;
    }

    /// <summary>
    /// 模块依赖解析器
    /// </summary>
    public class ModuleResolver
    {
        private readonly RegistryService _registry;

        public ModuleResolver(RegistryService registry)
        {
            _registry = registry;
        }

        /// <summary>
        /// 解析模块及其依赖
        /// </summary>
        public async UniTask<ResolveResult> ResolveAsync(string moduleId, string version, string registryId)
        {
            var result = new ResolveResult { Success = true };
            var resolved = new Dictionary<string, ResolvedModule>();
            var pending = new Queue<(string moduleId, string version, string registryId)>();

            pending.Enqueue((moduleId, version, registryId));

            while (pending.Count > 0)
            {
                var (mid, ver, rid) = pending.Dequeue();

                if (resolved.ContainsKey(mid))
                    continue;

                var registry = HubSettings.Instance.registries.Find(r => r.id == rid);
                if (registry == null)
                {
                    result.Warnings.Add($"找不到仓库: {rid}");
                    continue;
                }

                // 如果没有指定版本，使用最新版本
                if (string.IsNullOrEmpty(ver))
                {
                    var versions = await _registry.GetVersionsAsync(registry, mid);
                    ver = versions.LastOrDefault();
                    if (string.IsNullOrEmpty(ver))
                    {
                        result.Conflicts.Add($"找不到模块: {mid}");
                        result.Success = false;
                        continue;
                    }
                }

                var manifest = await _registry.GetManifestAsync(registry, mid, ver);
                if (manifest == null)
                {
                    result.Conflicts.Add($"找不到模块清单: {mid}@{ver}");
                    result.Success = false;
                    continue;
                }

                resolved[mid] = new ResolvedModule
                {
                    ModuleId = mid,
                    Version = ver,
                    RegistryId = rid,
                    Manifest = manifest
                };

                // 处理依赖
                if (manifest.dependencies != null)
                {
                    foreach (var dep in manifest.dependencies)
                    {
                        if (resolved.ContainsKey(dep.moduleId))
                        {
                            // 检查版本兼容性
                            var existingVer = resolved[dep.moduleId].Version;
                            if (!IsVersionCompatible(existingVer, dep.versionRange))
                            {
                                result.Conflicts.Add($"版本冲突: {dep.moduleId} 需要 {dep.versionRange}，但已解析 {existingVer}");
                                result.Success = false;
                            }
                            continue;
                        }

                        // 解析版本范围，获取最佳版本
                        var depVersions = await _registry.GetVersionsAsync(registry, dep.moduleId);
                        var bestVersion = FindBestVersion(depVersions, dep.versionRange);

                        if (string.IsNullOrEmpty(bestVersion))
                        {
                            result.Conflicts.Add($"找不到满足 {dep.versionRange} 的 {dep.moduleId} 版本");
                            result.Success = false;
                            continue;
                        }

                        pending.Enqueue((dep.moduleId, bestVersion, rid));
                    }
                }
            }

            // 按依赖顺序排序（被依赖的先安装）
            result.Modules = TopologicalSort(resolved.Values.ToList());
            return result;
        }

        /// <summary>
        /// 检查版本是否兼容
        /// </summary>
        public bool IsVersionCompatible(string version, string versionRange)
        {
            if (string.IsNullOrEmpty(versionRange))
                return true;

            var v = ParseVersion(version);

            // 支持的格式: ">=1.0.0", "^1.2.0", "~1.2.3", "1.0.0"
            if (versionRange.StartsWith(">="))
            {
                var min = ParseVersion(versionRange.Substring(2));
                return CompareVersions(v, min) >= 0;
            }
            if (versionRange.StartsWith("^"))
            {
                // ^1.2.3 表示 >=1.2.3 且 <2.0.0
                var min = ParseVersion(versionRange.Substring(1));
                var max = new[] { min[0] + 1, 0, 0 };
                return CompareVersions(v, min) >= 0 && CompareVersions(v, max) < 0;
            }
            if (versionRange.StartsWith("~"))
            {
                // ~1.2.3 表示 >=1.2.3 且 <1.3.0
                var min = ParseVersion(versionRange.Substring(1));
                var max = new[] { min[0], min[1] + 1, 0 };
                return CompareVersions(v, min) >= 0 && CompareVersions(v, max) < 0;
            }

            // 精确匹配
            var exact = ParseVersion(versionRange);
            return CompareVersions(v, exact) == 0;
        }

        /// <summary>
        /// 检查已安装模块的更新
        /// </summary>
        public async UniTask<List<ModuleUpdate>> CheckUpdatesAsync()
        {
            var updates = new List<ModuleUpdate>();
            var installed = InstalledModulesLock.Instance.modules;

            foreach (var module in installed)
            {
                var registry = HubSettings.Instance.registries.Find(r => r.id == module.registryId);
                if (registry == null) continue;

                var versions = await _registry.GetVersionsAsync(registry, module.moduleId);
                var latest = versions.LastOrDefault();

                if (!string.IsNullOrEmpty(latest) && CompareVersions(ParseVersion(latest), ParseVersion(module.version)) > 0)
                {
                    updates.Add(new ModuleUpdate
                    {
                        ModuleId = module.moduleId,
                        CurrentVersion = module.version,
                        LatestVersion = latest
                    });
                }
            }

            return updates;
        }

        private string FindBestVersion(List<string> versions, string versionRange)
        {
            if (versions == null || versions.Count == 0)
                return null;

            // 从高到低找第一个兼容的版本
            for (var i = versions.Count - 1; i >= 0; i--)
            {
                if (IsVersionCompatible(versions[i], versionRange))
                    return versions[i];
            }

            return null;
        }

        private int[] ParseVersion(string version)
        {
            var parts = Regex.Replace(version, @"[^\d.]", "").Split('.');
            var result = new int[3];
            for (var i = 0; i < Math.Min(parts.Length, 3); i++)
                int.TryParse(parts[i], out result[i]);
            return result;
        }

        private int CompareVersions(int[] v1, int[] v2)
        {
            for (var i = 0; i < 3; i++)
            {
                if (v1[i] != v2[i])
                    return v1[i].CompareTo(v2[i]);
            }
            return 0;
        }

        private List<ResolvedModule> TopologicalSort(List<ResolvedModule> modules)
        {
            // 简单实现：按依赖数量排序
            var moduleDict = modules.ToDictionary(m => m.ModuleId);
            var result = new List<ResolvedModule>();
            var visited = new HashSet<string>();

            void Visit(ResolvedModule module)
            {
                if (visited.Contains(module.ModuleId))
                    return;
                visited.Add(module.ModuleId);

                if (module.Manifest?.dependencies != null)
                {
                    foreach (var dep in module.Manifest.dependencies)
                    {
                        if (moduleDict.TryGetValue(dep.moduleId, out var depModule))
                            Visit(depModule);
                    }
                }

                result.Add(module);
            }

            foreach (var module in modules)
                Visit(module);

            return result;
        }
    }
}
#endif
