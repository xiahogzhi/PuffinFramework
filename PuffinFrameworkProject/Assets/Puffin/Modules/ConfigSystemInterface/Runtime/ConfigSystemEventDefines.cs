using System;
using Puffin.Runtime.Events.Interfaces;

namespace Puffin.Modules.ConfigSystemInterface.Runtime
{
    public struct ConfigSystemEventDefines
    {
        /// <summary>
        /// 卸载所有配置事件
        /// </summary>
        public struct OnUnloadAllEvent : IEventDefine
        {
        }

        /// <summary>
        /// 加载配置事件
        /// </summary>
        public struct OnLoadEvent : IEventDefine
        {
            /// <summary>
            /// 加载的类型
            /// </summary>
            public Type TargetType { set; get; }

            public OnLoadEvent(Type targetType)
            {
                TargetType = targetType;
            }
        }
    }
}