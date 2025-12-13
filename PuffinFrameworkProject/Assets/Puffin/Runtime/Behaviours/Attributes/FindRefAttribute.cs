using System;

namespace Puffin.Runtime.Behaviours.Attributes
{
    /// <summary>
    /// 路径引用特性 - 通过指定路径查找组件
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class FindRefAttribute : Attribute
    {
        public string Path { get; }
        public FindRefAttribute(string path = null) => Path = path;
    }
}