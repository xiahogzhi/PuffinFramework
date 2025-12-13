namespace Puffin.Runtime.Interfaces.SystemEvents
{
    public interface IRegisterEvent : IGameSystemEvent
    {
        void OnRegister();

        void OnUnRegister();
    }
}