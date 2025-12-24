using System;

namespace Puffin.Runtime.Core.Attributes
{
    /// <summary>
    /// 系统优先级特性 - 数值越小越先执行
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SystemPriorityAttribute : Attribute
    {
        public int Priority { get; }

        public SystemPriorityAttribute(int priority)
        {
            Priority = priority;
        }
    }
}
