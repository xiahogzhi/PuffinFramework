using System;

namespace Puffin.Runtime.Settings
{
    /// <summary>
    /// 标记设置类在 Puffin Settings 窗口中显示
    /// 使用此特性的 SettingsBase 子类将自动出现在 Puffin/Preference 菜单中
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PuffinSettingAttribute : Attribute
    {
        /// <summary>
        /// 在设置窗口中显示的名称
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 创建 PuffinSetting 特性
        /// </summary>
        /// <param name="displayName">显示名称，为空则使用类名</param>
        public PuffinSettingAttribute(string displayName = null)
        {
            DisplayName = displayName;
        }
    }
}
