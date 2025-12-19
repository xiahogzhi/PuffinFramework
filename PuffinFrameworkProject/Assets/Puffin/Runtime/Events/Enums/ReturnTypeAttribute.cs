using System;

namespace Puffin.Runtime.Events.Enums
{
    /// <summary>
    /// 标记事件的返回类型，用于事件系统的元数据标记
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ReturnTypeAttribute : Attribute
    {
        /// <summary>
        /// 事件返回类型
        /// </summary>
        public Type returnType { set; get; }

        /// <summary>
        /// 创建返回类型特性
        /// </summary>
        /// <param name="returnType">返回类型</param>
        public ReturnTypeAttribute(Type returnType)
        {
            this.returnType = returnType;
        }
    }
}