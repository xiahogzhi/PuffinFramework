
using Puffin.Runtime.Interfaces;

namespace Puffin.Modules.ResourcesSystemInterface.Runtime
{
    /// <summary>
    /// 资源加载器接口，定义资源加载的标准方法
    /// 可通过实现此接口对接不同的资源管理系统（如 Addressables）
    /// </summary>
    public interface IResourcesSystem : IResourcesLoader
    {
     
    }
}