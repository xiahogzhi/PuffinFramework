using System;

namespace Puffin.Runtime.Events.Enums
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ReturnTypeAttribute : Attribute
    {
        public Type returnType { set; get; }
     
        public ReturnTypeAttribute(Type returnType)
        {
            this.returnType = returnType;
        }
    }
}