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
            var pending = new Queue<(string moduleId, string version, string registryId, string requestedBy)>();

            pending.Enqueue((moduleId, version, registryId, null));

            while (pending.Count > 0)
            {
                var (mid, ver, rid, requestedBy) = pending.Dequeue();

                // 版本冲突处理
                if (resolved.TryGetValue(mid, out var existing))
                {
                    // 不同源的同名模块视为冲突（不是版本升级）
                    if (existing.RegistryId != rid)
                    {
                        result.Conflicts.Add($"模块源冲突: {mid} 来自不同仓库 [{existing.RegistryId}] vs [{rid}]");
                        result.Success = false;
                        continue;
                    }

                    // 同源模块：最高版本优先
                    if (!string.IsNullOrEmpty(ver) && !string.IsNullOrEmpty(existing.Version))
                    {
                        var cmp = CompareVersions(ParseVersion(ver), ParseVersion(existing.Version));
                        if (cmp > 0)
                        {
                            // 新版本更高，替换并重新解析依赖
                            result.Warnings.Add($"版本冲突: {mid} ({existing.Version} → {ver})，使用较高版本");
                            resolved.Remove(mid);
                        }
                        else if (cmp < 0)
                        {
                            // 已有版本更高，跳过
                            if (!string.IsNullOrEmpty(requestedBy))
                                result.Warnings.Add($"版本冲突: {requestedBy} 需要 {mid}@{ver}，但使用 {existing.Version}");
                            continue;
                        }
                        else
                        {
                            continue; // 版本相同
                        }
                    }
                    else
                    {
                        continue;
                    }
                }

                var registry = HubSettings.Instance.registries.Find(r => r.id == rid);
                if (registry == null)
                {
                    result.Conflicts.Add($"找不到仓库: {rid}");
                    result.Success = false;
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

                // 处理依赖（新格式优先）
                var allDeps = manifest.GetAllDependencies();
                foreach (var dep in allDeps)
                {
                    if (resolved.ContainsKey(dep.moduleId))
                        continue;

                    // 可选依赖：跳过未安装的
                    if (dep.optional)
                    {
                        var isInstalled = InstalledModulesLock.Instance.IsInstalled(dep.moduleId);
                        if (!isInstalled)
                        {
                            result.Warnings.Add($"跳过可选依赖: {dep.moduleId}");
                            continue;
                        }
                    }

                    // 确定使用的仓库源
                    var depRegistryId = !string.IsNullOrEmpty(dep.registryId) ? dep.registryId : rid;
                    var depRegistry = HubSettings.Instance.registries.Find(r => r.id == depRegistryId) ?? registry;

                    // 确定版本
                    var depVersion = dep.version;
                    if (string.IsNullOrEmpty(depVersion))
                    {
                        var depVersions = await _registry.GetVersionsAsync(depRegistry, dep.moduleId);
                        depVersion = depVersions?.LastOrDefault();
                    }

                    if (string.IsNullOrEmpty(depVersion))
                    {
                        if (dep.optional)
                        {
                            result.Warnings.Add($"找不到可选依赖: {dep.moduleId}");
                            continue;
                        }
                        result.Conflicts.Add($"找不到依赖模块: {dep.moduleId}");
                        result.Success = false;
                        continue;
                    }

                    pending.Enqueue((dep.moduleId, depVersion, depRegistryId, mid));
                }
            }

            // 按依赖顺序排序（被依赖的先安装）
            result.Modules = TopologicalSort(resolved.Values.ToList());
            return result;
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

                var deps = module.Manifest?.GetAllDependencies();
                if (deps != null)
                {
                    foreach (var dep in deps)
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
