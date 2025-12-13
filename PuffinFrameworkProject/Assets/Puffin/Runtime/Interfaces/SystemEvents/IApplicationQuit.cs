namespace Puffin.Runtime.Interfaces.SystemEvents
{
    public interface IApplicationQuit: IGameSystemEvent
    {
        public void OnApplicationQuit();
    }
}