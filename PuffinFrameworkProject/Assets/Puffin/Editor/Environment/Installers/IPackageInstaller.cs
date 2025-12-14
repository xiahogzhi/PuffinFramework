#if UNITY_EDITOR
using System.Threading;
using Cysharp.Threading.Tasks;
using Puffin.Editor.Environment.Core;

namespace Puffin.Editor.Environment.Installers
{
    public interface IPackageInstaller
    {
        DependencySource SupportedSource { get; }
        UniTask<bool> InstallAsync(DependencyDefinition dep, string destDir, Downloader downloader, CancellationToken ct = default);
        UniTask<bool> InstallFromCacheAsync(DependencyDefinition dep, string destDir, string cachePath, CancellationToken ct = default);
        bool IsInstalled(DependencyDefinition dep, string destDir);
    }
}
#endif
