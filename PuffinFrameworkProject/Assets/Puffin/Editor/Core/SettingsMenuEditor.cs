using Puffin.Runtime.Settings;
using UnityEditor;

namespace Puffin.Editor
{
    /// <summary>
    /// 设置菜单编辑器
    /// 为每个设置类添加菜单项需要在这里手动添加
    /// </summary>
    public static class SettingsMenuEditor
    {
        [MenuItem("Puffin Framework/Preference")]
        private static void SelectPreference()
        {
            PuffinSettings.SelectInEditor();
        }

        [MenuItem("Puffin Framework/Settings/Log Settings")]
        private static void SelectLogSettings()
        {
            LogSettings.SelectInEditor();
        }
    }
}
