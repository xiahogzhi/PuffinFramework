
using Puffin.Modules.ConfigSystemInterface.Runtime;

namespace Luban
{
    public abstract class BeanBase : ITypeId , IConfig
    {
        public abstract int GetTypeId();
    }
}
