using System;

namespace Puffin.Runtime.Events.Enums
{
    /// <summary>
    /// 标记事件属性为参数，用于事件系统的元数据标记
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ParamAttribute : Attribute
    {
    }
}