using System;

namespace Puffin.Runtime.Behaviours.Attributes
{
    /// <summary>
    /// 子节点引用特性 - 递归搜索所有子节点获取组件
    /// <para>支持数组/List类型，自动收集所有匹配组件</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class GetInChildrenAttribute : Attribute
    {
        public bool IncludeSelf { get; }
        public GetInChildrenAttribute(bool includeSelf = false) => IncludeSelf = includeSelf;
    }
}
