namespace Puffin.Runtime.Interfaces.SystemEvents
{
    /// <summary>
    /// 游戏系统实现这个接口可以实现Update,通过Runtime管理
    /// </summary>
    public interface IUpdate: IGameSystemEvent
    {
        public void OnUpdate(float deltaTime);
    }
}