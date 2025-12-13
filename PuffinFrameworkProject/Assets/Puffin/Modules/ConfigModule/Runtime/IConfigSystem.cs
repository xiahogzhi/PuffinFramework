using Puffin.Runtime.Interfaces;

namespace Puffin.Modules.ConfigModule.Runtime
{
    /// <summary>
    /// 配置系统接口
    /// </summary>
    public interface IConfigSystem : IGameSystem
    {
        /// <summary>
        /// 获取配置表
        /// </summary>
        T GetTable<T>() where T : class;

        /// <summary>
        /// 配置是否已加载
        /// </summary>
        bool IsLoaded { get; }
    }
}
