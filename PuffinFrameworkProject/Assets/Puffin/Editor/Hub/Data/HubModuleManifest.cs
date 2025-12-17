#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Puffin.Editor.Hub.Data
{
    /// <summary>
    /// 模块依赖配置
    /// </summary>
    [Serializable]
    public class ModuleDependency
    {
        public string moduleId;
        public string version;      // 固定版本，空表示最新
        public string registryId;   // 指定仓库源，空表示自动选择
        public bool optional;       // 是否可选依赖

        public ModuleDependency() { }
        public ModuleDependency(string id, string ver = null, bool opt = false, string registry = null)
        {
            // 支持 "moduleId@version" 格式
            if (id != null && id.Contains("@"))
            {
                var parts = id.Split('@');
                moduleId = parts[0];
                version = parts.Length > 1 ? parts[1] : ver;
            }
            else
            {
                moduleId = id;
                version = ver;
            }
            optional = opt;
            registryId = registry;
        }

        public override string ToString() => string.IsNullOrEmpty(version) ? moduleId : $"{moduleId}@{version}";
    }

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
        public List<string> dependencies = new();  // 模块依赖列表（兼容旧格式）
        public List<ModuleDependency> moduleDependencies = new();  // 新格式依赖列表
        public EnvironmentDependency[] envDependencies;
        public string downloadUrl;
        public string checksum;
        public long size;
        public string releaseNotes;
        public string publishedAt;

        /// <summary>
        /// 获取所有依赖（合并新旧格式）
        /// </summary>
        public List<ModuleDependency> GetAllDependencies()
        {
            var result = new List<ModuleDependency>();
            // 添加新格式依赖
            if (moduleDependencies != null)
                result.AddRange(moduleDependencies);
            // 添加旧格式依赖（转换为新格式）
            if (dependencies != null)
            {
                foreach (var dep in dependencies)
                {
                    // 解析 moduleId（支持 "moduleId@version" 格式）
                    var depId = dep.Contains("@") ? dep.Split('@')[0] : dep;
                    if (!result.Exists(d => d.moduleId == depId))
                        result.Add(new ModuleDependency(dep));
                }
            }
            return result;
        }

        /// <summary>
        /// 设置依赖（同时更新新旧格式）
        /// </summary>
        public void SetDependencies(List<ModuleDependency> deps)
        {
            moduleDependencies = deps ?? new List<ModuleDependency>();
            dependencies = new List<string>();
            foreach (var dep in moduleDependencies)
                dependencies.Add(dep.ToString());
        }
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
        public string extractPath;  // 从压缩包中提取的子路径
        public string[] requiredFiles;
        public bool optional;  // 是否可选
        public string[] targetFrameworks;  // NuGet 目标框架
        public string[] dllReferences;    // DLL 引用名称列表
        public string[] asmdefReferences; // 程序集定义引用名称列表
        public string asmdefName;  // ManualImport: 要检查的程序集定义名称
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
        public string updatedAt;
    }

    /// <summary>
    /// 模块加载状态
    /// </summary>
    public enum ModuleLoadState
    {
        NotLoaded,  // 未加载
        Loading,    // 加载中
        Loaded,     // 已加载
        Failed      // 加载失败
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
        public string RemoteVersion;      // 远程版本（用于比较）
        public string[] Tags;
        public string Author;
        public string RegistryId;         // 所属仓库ID（远程模块用）
        public string SourceRegistryId;   // 安装来源仓库ID（溯源）
        public string SourceRegistryName; // 安装来源仓库名称
        public bool IsInstalled;
        public bool HasUpdate;
        public List<string> Versions;     // 所有可用版本
        public string ReleaseNotes;       // 当前版本的更新日志
        public List<string> Dependencies; // 依赖列表
        public HubModuleManifest Manifest;
        public string UpdatedAt;          // 最后更新时间
        public ModuleLoadState LoadState; // 加载状态
    }
}
#endif
