using System;

namespace Puffin.Modules.GameDevKit.Runtime.Behaviours.Attributes
{
    /// <summary>
    /// 自动创建特性 - 如果组件不存在则自动添加
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class AutoCreateAttribute : Attribute { }
}
