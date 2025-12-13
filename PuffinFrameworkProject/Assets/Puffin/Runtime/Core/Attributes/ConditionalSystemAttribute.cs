using System;

namespace Puffin.Runtime.Core.Attributes
{
    /// <summary>
    /// 条件注册系统，只有定义了指定符号时才注册
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ConditionalSystemAttribute : Attribute
    {
        public string Symbol { get; }

        public ConditionalSystemAttribute(string symbol)
        {
            Symbol = symbol;
        }
    }
}
