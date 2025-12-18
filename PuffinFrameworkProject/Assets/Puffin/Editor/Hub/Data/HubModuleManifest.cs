#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Puffin.Editor.Hub.Data
{
    /// <summary>
    /// 模块依赖配置（仅用于安装依赖模块）
    /// </summary>
    [Serializable]
    public class ModuleDependency
    {
        public string moduleId;
        public string version;
        public string registryId;
        public bool optional;
    }

    /// <summary>
    /// 模块引用配置
    /// </summary>
    [Serializable]
    public class ModuleReference
    {
        public string moduleId;
        public bool includeRuntime = true;
        public bool includeEditor;
        public bool optional;
    }

    /// <summary>
    /// 程序集引用配置
    /// </summary>
    [Serializable]
    public class AsmdefReferenceConfig
    {
        public List<ModuleReference> moduleReferences = new();
        /// <summary>
        /// 引用列表，格式: xxx.asmdef 或 xxx.dll，#前缀为可选，分号分隔
        /// </summary>
        public string references = "";
    }

    /// <summary>
    /// 环境依赖（仅安装，不处理引用）
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
        public string asmdefName;  // ManualImport: 要检查的程序集定义名称
    }

    /// <summary>
    /// 模块清单
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
        public List<ModuleDependency> moduleDependencies = new();
        public EnvironmentDependency[] envDependencies;
        public AsmdefReferenceConfig references;
        public string downloadUrl;
        public string checksum;
        public long size;
        public string releaseNotes;
        public string publishedAt;

        /// <summary>
        /// 获取所有模块引用（模块依赖自动生成 + 手动配置）
        /// </summary>
        public List<ModuleReference> GetAllModuleReferences()
        {
            var result = new List<ModuleReference>();

            // 从非可选模块依赖自动生成引用
            if (moduleDependencies != null)
            {
                foreach (var dep in moduleDependencies)
                {
                    if (!dep.optional)
                        result.Add(new ModuleReference { moduleId = dep.moduleId, includeRuntime = true, includeEditor = true });
                }
            }

            // 添加手动配置的模块引用
            if (references?.moduleReferences != null)
            {
                foreach (var modRef in references.moduleReferences)
                {
                    var existing = result.Find(r => r.moduleId == modRef.moduleId);
                    if (existing != null)
                    {
                        existing.includeRuntime |= modRef.includeRuntime;
                        existing.includeEditor |= modRef.includeEditor;
                    }
                    else
                    {
                        result.Add(modRef);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 获取引用字符串
        /// </summary>
        public string GetReferences() => references?.references ?? "";

        /// <summary>
        /// 解析引用为 asmdef 和 dll 列表
        /// </summary>
        public void ParseReferences(out List<string> asmdefRefs, out List<string> dllRefs)
        {
            asmdefRefs = new List<string>();
            dllRefs = new List<string>();
            var text = GetReferences();
            if (string.IsNullOrWhiteSpace(text)) return;

            foreach (var item in text.Split(';'))
            {
                var trimmed = item.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var name = trimmed.StartsWith("#") ? trimmed.Substring(1) : trimmed;
                var prefix = trimmed.StartsWith("#") ? "#" : "";
                if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    dllRefs.Add(prefix + name);
                else if (name.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                    asmdefRefs.Add(prefix + name.Substring(0, name.Length - 7));
            }
        }
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

    [Serializable]
    public class ModuleVersionInfo
    {
        public string latest;
        public List<string> versions;
        public string updatedAt;
    }

    public enum ModuleLoadState { NotLoaded, Loading, Loaded, Failed }

    public class HubModuleInfo
    {
        public string ModuleId;
        public string DisplayName;
        public string Description;
        public string LatestVersion;
        public string InstalledVersion;
        public string RemoteVersion;
        public string[] Tags;
        public string Author;
        public string RegistryId;
        public string SourceRegistryId;
        public string SourceRegistryName;
        public bool IsInstalled;
        public bool HasUpdate;
        public List<string> Versions;
        public string ReleaseNotes;
        public List<ModuleDependency> Dependencies;
        public HubModuleManifest Manifest;
        public string UpdatedAt;
        public ModuleLoadState LoadState;
    }
}
#endif
