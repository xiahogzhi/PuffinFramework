#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Puffin.Editor.Hub
{
    /// <summary>
    /// 版本号工具类
    /// </summary>
    public static class VersionHelper
    {
        /// <summary>
        /// 比较两个版本号
        /// </summary>
        public static int Compare(string v1, string v2)
        {
            if (string.IsNullOrEmpty(v1)) return string.IsNullOrEmpty(v2) ? 0 : -1;
            if (string.IsNullOrEmpty(v2)) return 1;

            var parts1 = v1.Split('.');
            var parts2 = v2.Split('.');

            for (var i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
            {
                var p1 = i < parts1.Length && int.TryParse(parts1[i], out var n1) ? n1 : 0;
                var p2 = i < parts2.Length && int.TryParse(parts2[i], out var n2) ? n2 : 0;
                if (p1 != p2) return p1.CompareTo(p2);
            }
            return 0;
        }

        public static readonly IComparer<string> Comparer = new VersionComparer();

        private class VersionComparer : IComparer<string>
        {
            public int Compare(string x, string y) => VersionHelper.Compare(x, y);
        }
    }
}
#endif
