namespace Puffin.Runtime.Interfaces.SystemEvents
{
    public interface ILateUpdate : IGameSystemEvent
    {
        void OnLateUpdate(float deltaTime);
    }
}
