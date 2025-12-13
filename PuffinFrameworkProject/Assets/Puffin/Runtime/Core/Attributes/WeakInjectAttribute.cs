using System;

namespace Puffin.Runtime.Core.Attributes
{
    /// <summary>
    /// 弱引用注入 - 可选依赖，不存在时不报错
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class WeakInjectAttribute : Attribute
    {
    }
}
