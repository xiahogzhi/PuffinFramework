namespace Puffin.Runtime.Interfaces.SystemEvents
{
    /// <summary>
    /// 系统注册/注销事件接口，用于系统的初始化和清理
    /// </summary>
    public interface IRegisterEvent : IGameSystemEvent
    {
        /// <summary>
        /// 系统注册时调用，用于初始化
        /// </summary>
        void OnRegister();

        /// <summary>
        /// 系统注销时调用，用于清理资源
        /// </summary>
        void OnUnRegister();
    }
}