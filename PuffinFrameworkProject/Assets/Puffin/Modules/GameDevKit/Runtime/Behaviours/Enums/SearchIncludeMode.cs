using System;

namespace Puffin.Modules.GameDevKit.Runtime.Behaviours.Enums
{
    /// <summary>
    /// 引用搜索模式
    /// </summary>
    [Flags]
    public enum SearchIncludeMode
    {
        /// <summary>自身和子节点</summary>
        All = Self | Child,
        /// <summary>仅自身</summary>
        Self = 1 << 0,
        /// <summary>仅子节点</summary>
        Child = 1 << 1,
    }
}