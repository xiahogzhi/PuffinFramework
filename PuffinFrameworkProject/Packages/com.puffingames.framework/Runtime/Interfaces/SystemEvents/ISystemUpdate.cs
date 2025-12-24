namespace Puffin.Runtime.Interfaces.SystemEvents
{
    /// <summary>
    /// 每帧更新接口，实现此接口的系统将在每帧被调用
    /// </summary>
    public interface ISystemUpdate : ISystemEvent
    {
        /// <summary>
        /// 每帧更新回调
        /// </summary>
        /// <param name="deltaTime">距上一帧的时间间隔（秒）</param>
        void OnUpdate(float deltaTime);
    }
}