#if UNITY_EDITOR
using System;

namespace Puffin.Editor.Hub.Data
{
    /// <summary>
    /// 仓库源配置
    /// </summary>
    [Serializable]
    public class RegistrySource
    {
        public string id;
        public string name;
        public string url;           // GitHub: "owner/repo" 或完整 URL
        public string branch = "main";
        public bool isOfficial;
        public bool enabled = true;
        public string authToken;     // 可选: GitHub PAT (用于私有仓库/上传)

        /// <summary>
        /// 获取 registry.json 的 raw URL
        /// </summary>
        public string GetRegistryUrl()
        {
            if (url.StartsWith("http"))
                return url.TrimEnd('/') + "/registry.json";
            return $"https://raw.githubusercontent.com/{url}/{branch}/registry.json";
        }

        /// <summary>
        /// 获取模块清单的 raw URL
        /// </summary>
        public string GetManifestUrl(string moduleId, string version)
        {
            if (url.StartsWith("http"))
                return $"{url.TrimEnd('/')}/modules/{moduleId}/{version}/manifest.json";
            return $"https://raw.githubusercontent.com/{url}/{branch}/modules/{moduleId}/{version}/manifest.json";
        }

        /// <summary>
        /// 获取模块下载 URL
        /// </summary>
        public string GetDownloadUrl(string moduleId, string version, string fileName)
        {
            if (url.StartsWith("http"))
                return $"{url.TrimEnd('/')}/modules/{moduleId}/{version}/{fileName}";
            return $"https://raw.githubusercontent.com/{url}/{branch}/modules/{moduleId}/{version}/{fileName}";
        }
    }
}
#endif
