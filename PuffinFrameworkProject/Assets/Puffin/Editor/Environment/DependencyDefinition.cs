#if UNITY_EDITOR
using System;

namespace Puffin.Editor.Environment
{
    /// <summary>
    /// 依赖来源类型
    /// </summary>
    public enum DependencySource { NuGet, GitHubRepo, DirectUrl, GitHubRelease, UnityPackage, ManualImport }

    /// <summary>
    /// 依赖类型
    /// </summary>
    public enum DependencyType { DLL, Source, Tool }

    /// <summary>
    /// 依赖必要性
    /// </summary>
    public enum DependencyRequirement { Required, Optional }

    /// <summary>
    /// 依赖定义，描述一个外部依赖包的完整信息
    /// </summary>
    [Serializable]
    public class DependencyDefinition
    {
        public string id;
        public string displayName;
        public string version;
        public DependencySource source;
        public DependencyType type;
        public string url;
        public string extractPath;
        public string[] targetFrameworks;
        public string[] requiredFiles;
        public DependencyDefinition[] dependencies;
        public string installDir;
        public DependencyRequirement requirement;
        public string asmdefName;           // 安装时创建的程序集名称（源码类型）
    }
}
#endif
