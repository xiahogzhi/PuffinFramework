#if UNITY_EDITOR
using System;
using UnityEditor;

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

        // Token 保存在 EditorPrefs，不随文件同步
        private const string TokenKeyPrefix = "PuffinHub_Token_";

        public string authToken
        {
            get => EditorPrefs.GetString(TokenKeyPrefix + id, "");
            set => EditorPrefs.SetString(TokenKeyPrefix + id, value ?? "");
        }

        /// <summary>
        /// 获取 registry.json 的下载 URL（从仓库根目录）
        /// </summary>
        public string GetRegistryUrl()
        {
            if (url.StartsWith("http"))
                return url.TrimEnd('/') + "/registry.json";
            // GitHub raw: https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{path}
            return $"https://raw.githubusercontent.com/{url}/{branch}/registry.json";
        }

        /// <summary>
        /// 获取 registry.json 的 GitHub Contents API URL（无缓存）
        /// </summary>
        public string GetRegistryApiUrl()
        {
            if (url.StartsWith("http"))
                return null;  // 非 GitHub 仓库不支持
            // 使用 Contents API
            return $"https://api.github.com/repos/{url}/contents/registry.json?ref={branch}";
        }

        /// <summary>
        /// 是否为 GitHub 仓库
        /// </summary>
        public bool IsGitHubRepo => !url.StartsWith("http");

        /// <summary>
        /// 获取模块清单的下载 URL
        /// </summary>
        public string GetManifestUrl(string moduleId, string version)
        {
            if (url.StartsWith("http"))
                return $"{url.TrimEnd('/')}/modules/{moduleId}/{version}/manifest.json";
            // GitHub raw
            return $"https://raw.githubusercontent.com/{url}/{branch}/modules/{moduleId}/{version}/manifest.json";
        }

        /// <summary>
        /// 获取模块下载 URL
        /// </summary>
        public string GetDownloadUrl(string moduleId, string version, string fileName)
        {
            if (url.StartsWith("http"))
                return $"{url.TrimEnd('/')}/modules/{moduleId}/{fileName}";
            // GitHub raw
            return $"https://raw.githubusercontent.com/{url}/{branch}/modules/{moduleId}/{fileName}";
        }

        /// <summary>
        /// 获取 Contents API URL（用于绕过 CDN 缓存）
        /// </summary>
        public string GetContentsApiUrl(string path)
        {
            if (url.StartsWith("http"))
                return null;
            return $"https://api.github.com/repos/{url}/contents/{path}?ref={branch}";
        }
    }
}
#endif
