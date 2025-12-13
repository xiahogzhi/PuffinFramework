using Puffin.Runtime.Core.Configs;
using Puffin.Runtime.Interfaces;

namespace Puffin.Runtime.Core
{
    public class SetupContext
    {
        public IResourcesLoader ResourcesLoader { get; set; }

        public IPuffinLogger Logger { get; set; }

        public ScannerConfig ScannerConfig { set; get; }

        public RuntimeConfig runtimeConfig { set; get; }
    }
}