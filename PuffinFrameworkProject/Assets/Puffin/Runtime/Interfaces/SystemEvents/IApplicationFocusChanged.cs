namespace Puffin.Runtime.Interfaces.SystemEvents
{
    public interface IApplicationFocusChanged : IGameSystemEvent
    {
        void OnApplicationFocus(bool hasFocus);
    }
}