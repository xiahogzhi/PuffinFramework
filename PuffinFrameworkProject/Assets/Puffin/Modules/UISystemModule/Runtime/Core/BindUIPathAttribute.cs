using System;

namespace XFrameworks.Systems.UISystems.Core
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BindUIPathAttribute : Attribute
    {
        public string path { set; get; }

        public BindUIPathAttribute(string path)
        {
            this.path = path;
        }
    }
}