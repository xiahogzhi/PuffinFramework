using System;

namespace Puffin.Runtime.Core.Attributes
{
    /// <summary>
    /// 标记系统为自动注册，只有带此特性的系统才会被扫描器自动注册
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoRegisterAttribute : Attribute
    {
    }
}
