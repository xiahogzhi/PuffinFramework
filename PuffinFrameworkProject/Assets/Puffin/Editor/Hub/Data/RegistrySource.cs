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
        /// 获取 registry.json 的下载 URL（从 registry Release 的 Asset）
        /// </summary>
        public string GetRegistryUrl()
        {
            if (url.StartsWith("http"))
                return url.TrimEnd('/') + "/registry.json";
            // GitHub Releases: https://github.com/{owner}/{repo}/releases/download/{tag}/{filename}
            return $"https://github.com/{url}/releases/download/registry/registry.json";
        }

        /// <summary>
        /// 获取 registry.json 的 GitHub API URL（无缓存）
        /// </summary>
        public string GetRegistryApiUrl()
        {
            if (url.StartsWith("http"))
                return null;  // 非 GitHub 仓库不支持
            // 使用 Releases API 获取 registry Release 的 assets
            return $"https://api.github.com/repos/{url}/releases/tags/registry";
        }

        /// <summary>
        /// 是否为 GitHub 仓库
        /// </summary>
        public bool IsGitHubRepo => !url.StartsWith("http");

        /// <summary>
        /// 获取模块清单的下载 URL（从模块版本 Release 的 Asset）
        /// </summary>
        public string GetManifestUrl(string moduleId, string version)
        {
            if (url.StartsWith("http"))
                return $"{url.TrimEnd('/')}/modules/{moduleId}/{version}/manifest.json";
            // GitHub Releases: tag 格式为 {moduleId}-{version}
            return $"https://github.com/{url}/releases/download/{moduleId}-{version}/manifest.json";
        }

        /// <summary>
        /// 获取模块下载 URL（从模块版本 Release 的 Asset）
        /// </summary>
        public string GetDownloadUrl(string moduleId, string version, string fileName)
        {
            if (url.StartsWith("http"))
                return $"{url.TrimEnd('/')}/modules/{moduleId}/{version}/{fileName}";
            // GitHub Releases: tag 格式为 {moduleId}-{version}
            return $"https://github.com/{url}/releases/download/{moduleId}-{version}/{fileName}";
        }

        /// <summary>
        /// 获取 Release Asset 的 GitHub API URL（用于绕过 CDN 缓存下载）
        /// </summary>
        public string GetReleaseApiUrl(string tag)
        {
            if (url.StartsWith("http"))
                return null;
            return $"https://api.github.com/repos/{url}/releases/tags/{tag}";
        }
    }
}
#endif
