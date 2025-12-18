using System;

namespace Puffin.Runtime.Settings
{
    /// <summary>
    /// 标记设置类在 Puffin Settings 窗口中显示
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PuffinSettingAttribute : Attribute
    {
        public string DisplayName { get; }

        public PuffinSettingAttribute(string displayName = null)
        {
            DisplayName = displayName;
        }
    }
}
