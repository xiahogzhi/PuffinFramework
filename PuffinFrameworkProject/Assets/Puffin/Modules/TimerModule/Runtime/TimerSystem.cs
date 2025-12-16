using Puffin.Runtime.Core.Attributes;
using Puffin.Runtime.Interfaces;
using Puffin.Runtime.Interfaces.SystemEvents;

namespace Puffin.Modules.TimerModule.Runtime
{
    /// <summary>
    /// 定时器系统
    /// <para>自动注册的系统，负责驱动所有 Timer 的更新</para>
    /// <para>优先级设为 -100，确保在其他系统之前更新</para>
    /// </summary>
    [AutoRegister]
    public class TimerSystem : IGameSystem, IUpdate
    {
        /// <summary>系统优先级，越低越先执行</summary>
        public int priority => -100;

        /// <summary>
        /// 每帧更新所有定时器
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            global::Puffin.Modules.TimerModule.Runtime.Timer.UpdateAll();
        }

       
    }
} 
