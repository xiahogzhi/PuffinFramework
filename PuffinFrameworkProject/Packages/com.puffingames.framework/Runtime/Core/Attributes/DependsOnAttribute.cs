using System;

namespace Puffin.Runtime.Core.Attributes
{
    /// <summary>
    /// 标记系统依赖，确保依赖的系统先初始化
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class DependsOnAttribute : Attribute
    {
        public Type DependencyType { get; }

        public DependsOnAttribute(Type dependencyType)
        {
            DependencyType = dependencyType;
        }
    }
}
