#if UNITY_EDITOR
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Puffin.Editor.Environment.Core;
using UnityEngine;

namespace Puffin.Editor.Environment.Installers
{
    /// <summary>
    /// Unity Package Manager 安装器
    /// 通过修改 Packages/manifest.json 添加依赖
    /// </summary>
    public class UnityPackageInstaller : IPackageInstaller
    {
        public DependencySource SupportedSource => DependencySource.UnityPackage;

        public async UniTask<bool> InstallAsync(DependencyDefinition dep, string destDir, Downloader downloader, CancellationToken ct = default)
        {
            var manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            if (!File.Exists(manifestPath))
            {
                Debug.LogError("[UnityPackageInstaller] manifest.json not found");
                return false;
            }

            var json = File.ReadAllText(manifestPath);
            var manifest = JObject.Parse(json);
            var dependencies = manifest["dependencies"] as JObject;

            if (dependencies == null)
            {
                Debug.LogError("[UnityPackageInstaller] dependencies not found in manifest.json");
                return false;
            }

            var packageId = dep.id;
            var packageVersion = GetPackageValue(dep);

            if (dependencies[packageId] != null)
            {
                Debug.Log($"[UnityPackageInstaller] Package {packageId} already exists, updating to {packageVersion}");
            }

            dependencies[packageId] = packageVersion;

            var newJson = manifest.ToString(Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(manifestPath, newJson);

            Debug.Log($"[UnityPackageInstaller] Added {packageId}: {packageVersion}");

            await UniTask.Yield();
            return true;
        }

        public bool Uninstall(DependencyDefinition dep, string destDir)
        {
            var manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            if (!File.Exists(manifestPath))
                return false;

            var json = File.ReadAllText(manifestPath);
            var manifest = JObject.Parse(json);
            var dependencies = manifest["dependencies"] as JObject;

            if (dependencies == null || dependencies[dep.id] == null)
                return true;

            dependencies.Remove(dep.id);

            var newJson = manifest.ToString(Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(manifestPath, newJson);

            Debug.Log($"[UnityPackageInstaller] Removed {dep.id}");
            return true;
        }

        public UniTask<bool> InstallFromCacheAsync(DependencyDefinition dep, string destDir, string cachePath, CancellationToken ct = default)
        {
            return InstallAsync(dep, destDir, null, ct);
        }

        public bool IsInstalled(DependencyDefinition dep, string destDir)
        {
            var manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            if (!File.Exists(manifestPath))
                return false;

            var json = File.ReadAllText(manifestPath);
            var manifest = JObject.Parse(json);
            var dependencies = manifest["dependencies"] as JObject;

            if (dependencies == null)
                return false;

            var existing = dependencies[dep.id];
            if (existing == null)
                return false;

            if (!string.IsNullOrEmpty(dep.version))
            {
                var installedVersion = existing.Value<string>();
                return installedVersion == GetPackageValue(dep);
            }

            return true;
        }

        private string GetPackageValue(DependencyDefinition dep)
        {
            if (!string.IsNullOrEmpty(dep.url))
            {
                if (!string.IsNullOrEmpty(dep.version) && !dep.url.Contains("#"))
                    return $"{dep.url}#{dep.version}";
                return dep.url;
            }
            return dep.version ?? "latest";
        }
    }
}
#endif
