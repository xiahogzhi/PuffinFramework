using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Puffin.Runtime.Interfaces;
using UnityEngine;

namespace Puffin.Boot.Runtime
{
    /// <summary>
    /// Bootstrap 扫描器，负责发现和实例化所有 IBootstrap 实现
    /// </summary>
    public class BootstrapScanner
    {
        private readonly IPuffinLogger _logger;
        private readonly LauncherSetting _setting;

        public BootstrapScanner(IPuffinLogger logger, LauncherSetting setting)
        {
            _logger = logger;
            _setting = setting;
        }

        /// <summary>
        /// 扫描并创建所有 Bootstrap 实例，按优先级排序
        /// </summary>
        public List<IBootstrap> ScanAndCreate()
        {
            var bootstrapTypes = ScanBootstrapTypes();
            var bootstraps = new List<IBootstrap>();

            foreach (var type in bootstrapTypes)
            {
                try
                {
                    var instance = Activator.CreateInstance(type) as IBootstrap;
                    if (instance != null)
                    {
                        bootstraps.Add(instance);
                        _logger?.Info($"[Bootstrap] 发现启动器: {type.Name} (优先级: {instance.Priority})");
                    }
                }
                catch (Exception e)
                {
                    _logger?.Error($"[Bootstrap] 创建启动器失败: {type.Name}\n{e}");
                }
            }

            // 按优先级排序（数值越小越先执行）
            bootstraps.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            return bootstraps;
        }

        /// <summary>
        /// 扫描所有实现 IBootstrap 的类型
        /// </summary>
        private Type[] ScanBootstrapTypes()
        {
            var types = new List<Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!ShouldScanAssembly(assembly))
                    continue;

                try
                {
                    var assemblyTypes = assembly.GetTypes()
                        .Where(t => typeof(IBootstrap).IsAssignableFrom(t)
                                    && !t.IsAbstract
                                    && !t.IsInterface
                                    && t.GetConstructor(Type.EmptyTypes) != null); // 必须有无参构造函数

                    types.AddRange(assemblyTypes);
                }
                catch (Exception e)
                {
                    _logger?.Warning($"[Bootstrap] 扫描程序集失败: {assembly.GetName().Name}\n{e.Message}");
                }
            }

            return types.ToArray();
        }

        /// <summary>
        /// 判断是否应该扫描该程序集
        /// </summary>
        private bool ShouldScanAssembly(Assembly assembly)
        {
            var name = assembly.GetName().Name;

            // 排除系统程序集
            if (_setting != null && _setting.excludeAssemblyPrefixes != null)
            {
                foreach (var prefix in _setting.excludeAssemblyPrefixes)
                {
                    if (name.StartsWith(prefix))
                        return false;
                }
            }

            // 如果指定了程序集列表，只扫描指定的
            if (_setting != null && _setting.scanAssemblyPrefixes != null && _setting.scanAssemblyPrefixes.Count > 0)
            {
                return _setting.scanAssemblyPrefixes.Any(p => name.StartsWith(p));
            }

            return true;
        }
    }
}
