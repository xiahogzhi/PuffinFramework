using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Puffin.Runtime.Core.Attributes;
using Puffin.Runtime.Core.Configs;
using Puffin.Runtime.Interfaces;
using Puffin.Runtime.Settings;

namespace Puffin.Runtime.Core
{
    /// <summary>
    /// 游戏系统扫描器
    /// </summary>
    public class GameSystemScanner
    {
        private readonly IPuffinLogger _logger;
        private readonly ScannerConfig _config;

        public GameSystemScanner(IPuffinLogger logger, ScannerConfig config = null)
        {
            _logger = logger;
            _config = config ?? new ScannerConfig();
        }

        public async UniTask<Type[]> ScanAsync()
        {
            var result = new List<Type>();
            var assemblies = GetAssembliesToScan();

            foreach (var assembly in assemblies)
            {
                await UniTask.Yield();

                try
                {
                    var types = ScanAssembly(assembly);
                    foreach (var type in types)
                    {
                        result.Add(type);
                        _logger.Info($"扫描到系统: {type.FullName}");
                    }
                }
                catch (Exception)
                {
                    // 跳过无法加载的程序集
                }
            }

            return result.ToArray();
        }

        private IEnumerable<Assembly> GetAssembliesToScan()
        {
            // 如果指定了程序集，直接使用
            if (_config.Assemblies.Count > 0)
                return _config.Assemblies;

            // 否则扫描所有程序集，应用过滤
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(ShouldScanAssembly);
        }

        private bool ShouldScanAssembly(Assembly assembly)
        {
            var name = assembly.GetName().Name;

            // 排除系统程序集
            if (_config.ExcludeAssemblyPrefixes.Any(p => name.StartsWith(p)))
                return false;

            // 如果指定了前缀过滤，检查是否匹配
            if (_config.AssemblyPrefixes.Count > 0)
                return _config.AssemblyPrefixes.Any(p => name.StartsWith(p));

            return true;
        }

        private Type[] ScanAssembly(Assembly assembly)
        {
            return assembly.GetTypes()
                .Where(IsValidSystemType)
                .ToArray();
        }

        private bool IsValidSystemType(Type type)
        {
            // 基本条件
            if (!typeof(IGameSystem).IsAssignableFrom(type))
                return false;
            if (type.IsAbstract || type.IsInterface)
                return false;

            // AutoRegister 检查
            if (_config.RequireAutoRegister && type.GetCustomAttribute<AutoRegisterAttribute>() == null)
                return false;

            // 检查系统注册配置（编辑器禁用的系统）
            var registrySettings = SystemRegistrySettings.Instance;
            if (registrySettings != null && registrySettings.IsSystemDisabled(type))
            {
                _logger.Info($"系统被禁用（通过 SystemRegistry）: {type.FullName}");
                return false;
            }

            // 检查模块是否启用
            var moduleRegistry = ModuleRegistrySettings.Instance;
            if (moduleRegistry != null)
            {
                var assemblyName = type.Assembly.GetName().Name;
                if (moduleRegistry.IsAssemblyDisabled(assemblyName))
                {
                    _logger.Info($"系统被禁用（模块禁用）: {type.FullName}");
                    return false;
                }
            }

            // 自定义过滤器
            if (_config.TypeFilter != null && !_config.TypeFilter(type))
                return false;

            return true;
        }
    }
}
