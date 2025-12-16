#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Puffin.Editor.Hub.Data
{
    /// <summary>
    /// Hub 设置
    /// </summary>
    public class HubSettings : ScriptableObject
    {
        private const string SettingsPath = "Assets/Puffin/Editor/Hub/HubSettings.asset";

        [Header("仓库源")]
        public List<RegistrySource> registries = new()
        {
            new RegistrySource
            {
                id = "official",
                name = "Puffin Official",
                url = "puffin-framework/puffin-modules",
                branch = "main",
                isOfficial = true,
                enabled = true
            }
        };

        [Header("缓存设置")]
        [Tooltip("索引缓存过期时间（小时）")]
        public int cacheExpireHours = 24;

        [Tooltip("使用系统代理")]
        public bool useSystemProxy = true;

        [Header("下载设置")]
        [Tooltip("使用 GitHub API 下载（绕过 CDN 缓存，但有速率限制）")]
        public bool useGitHubApiDownload = false;

        private static HubSettings _instance;

        public static HubSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = AssetDatabase.LoadAssetAtPath<HubSettings>(SettingsPath);
                    if (_instance == null)
                    {
                        _instance = CreateInstance<HubSettings>();
                        var dir = Path.GetDirectoryName(SettingsPath);
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        AssetDatabase.CreateAsset(_instance, SettingsPath);
                        AssetDatabase.SaveAssets();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 缓存目录
        /// </summary>
        public static string CacheDir => Path.Combine(Application.dataPath, "../Library/PuffinHubCache");

        /// <summary>
        /// 锁定文件路径
        /// </summary>
        public static string LockFilePath => Path.Combine(Application.dataPath, "../Library/PuffinHub/installed.json");

        public void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 添加仓库源
        /// </summary>
        public void AddRegistry(RegistrySource source)
        {
            if (registries.Exists(r => r.id == source.id))
                return;
            registries.Add(source);
            Save();
        }

        /// <summary>
        /// 移除仓库源
        /// </summary>
        public void RemoveRegistry(string id)
        {
            registries.RemoveAll(r => r.id == id && !r.isOfficial);
            Save();
        }

        /// <summary>
        /// 获取启用的仓库源
        /// </summary>
        public List<RegistrySource> GetEnabledRegistries()
        {
            return registries.FindAll(r => r.enabled);
        }

        /// <summary>
        /// 是否有任何仓库配置了 token（开发者模式）
        /// </summary>
        public bool HasAnyToken()
        {
            return registries.Exists(r => r.enabled && !string.IsNullOrEmpty(r.authToken));
        }

        /// <summary>
        /// 获取有 token 的仓库源
        /// </summary>
        public List<RegistrySource> GetRegistriesWithToken()
        {
            return registries.FindAll(r => r.enabled && !string.IsNullOrEmpty(r.authToken));
        }

        /// <summary>
        /// 检查指定仓库是否有 token
        /// </summary>
        public bool HasToken(string registryId)
        {
            var registry = registries.Find(r => r.id == registryId);
            return registry != null && !string.IsNullOrEmpty(registry.authToken);
        }
    }
}
#endif
