using Puffin.Runtime.Events.Interfaces;

namespace Puffin.Runtime.Core
{
    /// <summary>
    /// 系统事件定义集合
    /// </summary>
    public struct SystemEventDefines
    {
        /// <summary>
        /// 游戏初始化完成事件，在所有系统注册和初始化完成后触发
        /// </summary>
        public struct OnGameInitialized : IEventDefine
        {
        }
    }
}