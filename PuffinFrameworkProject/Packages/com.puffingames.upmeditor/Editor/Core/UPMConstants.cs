#if UNITY_EDITOR
using UnityEngine;

namespace PuffinGames.UPMEditor
{
    /// <summary>
    /// Constants for UPM Editor
    /// </summary>
    public static class UPMConstants
    {
        // Paths
        public const string AssetsPath = "Assets";
        public const string PackagesPath = "Packages";
        public const string PackageJsonFileName = "package.json";

        // Menu paths
        public const string ContextMenuRoot = "Assets/UPM/";
        public const string ToolsMenuRoot = "Puffin Games/UPM/";

        // Default values
        public static string DefaultUnityVersion => Application.unityVersion.Substring(0, Application.unityVersion.LastIndexOf('.'));
        public const string DefaultVersion = "1.0.0";

        // EditorPrefs keys
        public const string PrefsPrefix = "UPMEditor_";
        public const string PrefsRecentPackages = PrefsPrefix + "RecentPackages";

        // File templates
        public const string RuntimeAsmdefSuffix = "";
        public const string EditorAsmdefSuffix = ".Editor";
        public const string TestsAsmdefSuffix = ".Tests";
    }
}
#endif
