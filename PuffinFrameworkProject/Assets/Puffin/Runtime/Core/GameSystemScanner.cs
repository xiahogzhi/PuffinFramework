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

        /// <summary>
        /// 创建游戏系统扫描器实例
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="config">扫描配置，为空则使用默认配置</param>
        public GameSystemScanner(IPuffinLogger logger, ScannerConfig config = null)
        {
            _logger = logger;
            _config = config ?? new ScannerConfig();
        }

        /// <summary>
        /// 异步扫描所有符合条件的游戏系统类型
        /// </summary>
        /// <returns>扫描到的系统类型数组</returns>
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

        /// <summary>
        /// 获取需要扫描的程序集列表
        /// </summary>
        private IEnumerable<Assembly> GetAssembliesToScan()
        {
            // 如果指定了程序集，直接使用
            if (_config.Assemblies.Count > 0)
                return _config.Assemblies;

            // 否则扫描所有程序集，应用过滤
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(ShouldScanAssembly);
        }

        /// <summary>
        /// 判断程序集是否应该被扫描
        /// </summary>
        /// <param name="assembly">待检查的程序集</param>
        /// <returns>是否应该扫描该程序集</returns>
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

        /// <summary>
        /// 扫描单个程序集中的系统类型
        /// </summary>
        /// <param name="assembly">要扫描的程序集</param>
        /// <returns>该程序集中的有效系统类型数组</returns>
        private Type[] ScanAssembly(Assembly assembly)
        {
            return assembly.GetTypes()
                .Where(IsValidSystemType)
                .ToArray();
        }

        /// <summary>
        /// 验证类型是否为有效的游戏系统类型
        /// </summary>
        /// <param name="type">待验证的类型</param>
        /// <returns>是否为有效的系统类型</returns>
        private bool IsValidSystemType(Type type)
        {
            // 基本条件
            if (!typeof(ISystem).IsAssignableFrom(type))
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
