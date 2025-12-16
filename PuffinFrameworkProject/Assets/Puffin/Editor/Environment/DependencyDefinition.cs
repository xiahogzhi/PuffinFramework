#if UNITY_EDITOR
using System;

namespace Puffin.Editor.Environment
{
    public enum DependencySource { NuGet, GitHubRepo, DirectUrl, GitHubRelease, UnityPackage }
    public enum DependencyType { DLL, Source, Tool }
    public enum DependencyRequirement { Required, Optional }

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
        public string[] dllReferences;      // DLL 引用名称列表
        public string[] asmdefReferences;   // 程序集定义引用名称列表
    }
}
#endif
