using System;

namespace Puffin.Runtime.Behaviours.Attributes
{
    /// <summary>
    /// 必填验证特性 - 未赋值时在编辑器显示警告
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class RequiredAttribute : Attribute
    {
        public string Message { get; }
        public RequiredAttribute(string message = null) => Message = message;
    }
}
