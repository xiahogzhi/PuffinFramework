using System;
using Puffin.Modules.GameDevKit.Runtime.Behaviours.Enums;

namespace Puffin.Modules.GameDevKit.Runtime.Behaviours.Attributes
{
    /// <summary>
    /// 自动引用特性 - 从自身或子节点获取第一个匹配的组件
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class AnyRefAttribute : Attribute
    {
        public SearchIncludeMode Mode { get; }
        public AnyRefAttribute(SearchIncludeMode mode = SearchIncludeMode.All) => Mode = mode;
    }
}