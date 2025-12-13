namespace Puffin.Runtime.Interfaces.SystemEvents
{
    public interface IApplicationPause : IGameSystemEvent
    {
        void OnApplicationPause(bool pause);
    }
}
