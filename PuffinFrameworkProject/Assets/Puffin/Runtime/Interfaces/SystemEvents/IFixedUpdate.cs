namespace Puffin.Runtime.Interfaces.SystemEvents
{
    public interface IFixedUpdate : IGameSystemEvent
    {
        public void OnFixedUpdate(float deltaTime);
    }
}