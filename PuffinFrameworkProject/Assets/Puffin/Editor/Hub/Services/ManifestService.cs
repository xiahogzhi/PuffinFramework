#if UNITY_EDITOR
using System.IO;
using Newtonsoft.Json;
using Puffin.Editor.Hub;
using Puffin.Editor.Hub.Data;
using UnityEngine;

namespace Puffin.Editor.Hub.Services
{
    /// <summary>
    /// 模块清单服务
    /// </summary>
    public static class ManifestService
    {
        public static string GetModulesPath() => Path.Combine(Application.dataPath, HubConstants.ModulesRelativePath);

        public static string GetModulePath(string moduleId) => Path.Combine(GetModulesPath(), moduleId);

        public static string GetManifestPath(string moduleId) => Path.Combine(GetModulePath(moduleId), HubConstants.ManifestFileName);

        public static string GetManifestPathFromDir(string moduleDir) => Path.Combine(moduleDir, HubConstants.ManifestFileName);

        public static HubModuleManifest Load(string manifestPath)
        {
            if (!File.Exists(manifestPath)) return null;
            return JsonConvert.DeserializeObject<HubModuleManifest>(File.ReadAllText(manifestPath));
        }

        public static HubModuleManifest LoadByModuleId(string moduleId) => Load(GetManifestPath(moduleId));

        public static void Save(string manifestPath, HubModuleManifest manifest)
        {
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest, Formatting.Indented));
        }

        public static bool ModuleExists(string moduleId) => Directory.Exists(GetModulePath(moduleId));
    }
}
#endif
