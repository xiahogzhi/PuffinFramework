using System;

namespace Puffin.Runtime.Core.Attributes
{
    /// <summary>
    /// 标记字段或属性需要依赖注入
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class InjectAttribute : Attribute
    {
    }
}
