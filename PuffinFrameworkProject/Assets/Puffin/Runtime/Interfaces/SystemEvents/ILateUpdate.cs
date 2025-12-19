namespace Puffin.Runtime.Interfaces.SystemEvents
{
    /// <summary>
    /// 延迟更新接口，实现此接口的系统将在所有 Update 之后被调用
    /// 适用于相机跟随等需要在其他更新完成后执行的逻辑
    /// </summary>
    public interface ILateUpdate : IGameSystemEvent
    {
        /// <summary>
        /// 延迟更新回调
        /// </summary>
        /// <param name="deltaTime">距上一帧的时间间隔（秒）</param>
        void OnLateUpdate(float deltaTime);
    }
}
