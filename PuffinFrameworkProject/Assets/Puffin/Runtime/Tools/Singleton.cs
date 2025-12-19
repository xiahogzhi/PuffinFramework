namespace Puffin.Runtime.Tools
{
    /// <summary>
    /// 泛型单例基类，提供线程安全的单例实现
    /// 通过静态构造函数确保实例在首次访问时创建
    /// </summary>
    /// <typeparam name="T">单例类型，必须有无参构造函数</typeparam>
    public class Singleton<T> where T : new()
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static T Instance { private set; get; }

        static Singleton()
        {
            Instance = new T();
        }
    }
}