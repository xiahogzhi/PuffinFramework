using System;

namespace Puffin.Runtime.Core.Attributes
{
    /// <summary>
    /// 系统别名特性 - 通过字符串名称获取系统
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SystemAliasAttribute : Attribute
    {
        public string Alias { get; }

        public SystemAliasAttribute(string alias)
        {
            Alias = alias;
        }
    }
}
