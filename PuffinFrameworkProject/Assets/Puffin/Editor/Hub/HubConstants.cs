#if UNITY_EDITOR
namespace Puffin.Editor.Hub
{
    /// <summary>
    /// Hub 常量定义
    /// </summary>
    public static class HubConstants
    {
        public const string ModulesRelativePath = "Puffin/Modules";
        public const string ManifestFileName = "module.json";
        public const string RuntimeFolder = "Runtime";
        public const string EditorFolder = "Editor";
        public const string ResourcesFolder = "Resources";
        public const string AsmdefExtension = ".asmdef";

        public const string FrameworkRuntimeAssembly = "PuffinFramework.Runtime";
        public const string FrameworkEditorAssembly = "PuffinFramework.Editor";
    }
}
#endif
