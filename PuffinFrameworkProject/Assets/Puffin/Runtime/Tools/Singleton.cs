namespace Puffin.Runtime.Tools
{
    public class Singleton<T> where T : new()
    {
        public static T Instance { private set; get; }


        static Singleton()
        {
            Instance = new T();
        }
    }
}