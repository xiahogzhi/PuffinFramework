using System;

namespace Puffin.Runtime.Behaviours.Attributes
{
    /// <summary>
    /// 父节点引用特性 - 从父节点链获取组件
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class GetInParentAttribute : Attribute
    {
        public bool IncludeSelf { get; }
        public GetInParentAttribute(bool includeSelf = false) => IncludeSelf = includeSelf;
    }
}
