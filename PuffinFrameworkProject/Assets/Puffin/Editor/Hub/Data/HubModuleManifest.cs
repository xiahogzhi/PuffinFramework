#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Puffin.Editor.Hub.Data
{
    /// <summary>
    /// 模块清单 - 描述模块的元数据
    /// </summary>
    [Serializable]
    public class HubModuleManifest
    {
        public string moduleId;
        public string displayName;
        public string version;
        public string author;
        public string description;
        public string[] tags;
        public string unityVersion;
        public string puffinVersion;
        public ModuleDependency[] dependencies;
        public EnvironmentDependency[] envDependencies;
        public string downloadUrl;
        public string checksum;
        public long size;
        public string releaseNotes;
        public string publishedAt;
    }

    /// <summary>
    /// 模块依赖
    /// </summary>
    [Serializable]
    public class ModuleDependency
    {
        public string moduleId;
        public string versionRange;  // 语义化版本范围: ">=1.0.0", "^1.2.0"
    }

    /// <summary>
    /// 环境依赖 - 复用现有依赖系统
    /// </summary>
    [Serializable]
    public class EnvironmentDependency
    {
        public string id;
        public int source;  // DependencySource
        public int type;    // DependencyType
        public string url;
        public string version;
        public string installDir;
        public string[] requiredFiles;
    }

    /// <summary>
    /// 仓库索引
    /// </summary>
    [Serializable]
    public class RegistryIndex
    {
        public string name;
        public string version;
        public Dictionary<string, ModuleVersionInfo> modules;
    }

    /// <summary>
    /// 模块版本信息
    /// </summary>
    [Serializable]
    public class ModuleVersionInfo
    {
        public string latest;
        public List<string> versions;
    }

    /// <summary>
    /// Hub 显示用的模块信息
    /// </summary>
    public class HubModuleInfo
    {
        public string ModuleId;
        public string DisplayName;
        public string Description;
        public string LatestVersion;      // 远程最新版本
        public string InstalledVersion;   // 本地安装版本
        public string RemoteVersion;      // 远程版本（用于本地模块比较）
        public string[] Tags;
        public string Author;
        public string RegistryId;         // 所属仓库ID（远程模块用）
        public string SourceRegistryId;   // 安装来源仓库ID（溯源）
        public string SourceRegistryName; // 安装来源仓库名称
        public bool IsInstalled;
        public bool HasUpdate;
        public bool IsLocal;              // 仅本地存在（无远程匹配）
        public bool HasRemote;            // 是否有远程版本
        public List<string> Versions;     // 所有可用版本
        public string ReleaseNotes;       // 当前版本的更新日志
        public HubModuleManifest Manifest;
    }
}
#endif
