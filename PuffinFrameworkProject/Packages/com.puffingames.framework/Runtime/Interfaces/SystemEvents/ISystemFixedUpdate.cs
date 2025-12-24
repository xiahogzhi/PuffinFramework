namespace Puffin.Runtime.Interfaces.SystemEvents
{
    /// <summary>
    /// 固定时间步更新接口，实现此接口的系统将在固定时间间隔被调用
    /// 适用于物理计算等需要固定时间步的逻辑
    /// </summary>
    public interface ISystemFixedUpdate : ISystemEvent
    {
        /// <summary>
        /// 固定时间步更新回调
        /// </summary>
        /// <param name="deltaTime">固定时间间隔（秒）</param>
        void OnFixedUpdate(float deltaTime);
    }
}