using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Puffin.Runtime.Core;
using Puffin.Runtime.Core.Attributes;
using Puffin.Runtime.Core.Configs;
using Puffin.Runtime.Interfaces;
using Puffin.Runtime.Interfaces.SystemEvents;
using Puffin.Runtime.Settings;
using Puffin.Runtime.Tools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine; 

namespace Puffin.Boot.Runtime
{
    /// <summary>
    /// 框架启动器，负责初始化和启动 PuffinFramework
    /// 在场景中放置此组件即可自动启动框架
    /// 支持运行时自动初始化和编辑器模式下的系统初始化
    /// </summary>
    public class Launcher : MonoBehaviour
    {
        /// <summary>
        /// 运行时自动初始化入口，在场景加载后自动调用
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeRuntime()
        {
            var launcher = FindAnyObjectByType<Launcher>();
            if (launcher != null)
            {
                launcher.Setup();

                var settings = PuffinSettings.Instance;
                if (settings != null && settings.autoInitialize)
                {
                    launcher.StartAsync();
                }
            }
            else
                throw new Exception("Could not find launcher!");
        }

#if UNITY_EDITOR

        [InitializeOnLoadMethod]
        private static void AutoInitializeEditor()
        {
            EditorApplication.delayCall += InitializeEditorSystems;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // 监听设置变化
            ModuleRegistrySettings.OnSettingsChanged += OnRegistrySettingsChanged;
            SystemRegistrySettings.OnSettingsChanged += OnRegistrySettingsChanged;
        }

        private static GameSystemRuntime _editorRuntime;
        private static ScannerConfig _cachedScannerConfig;

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // 从 Play 模式退出后重新初始化编辑器系统
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                _editorRuntime = null;
                EditorApplication.delayCall += InitializeEditorSystems;
            }
        }

        /// <summary>
        /// 编辑器模式下的 Runtime（非 Play 模式时使用）
        /// </summary>
        public static GameSystemRuntime EditorRuntime => _editorRuntime;

        private static void InitializeEditorSystems()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            InitializeEditorSystemsAsync().Forget();
        }

        private static async UniTask InitializeEditorSystemsAsync()
        {
            try
            {
                await UniTask.Yield();

                var settings = PuffinSettings.Instance;
                var scannerConfig = settings.ToScannerConfig();
                var runtimeConfig = settings.ToRuntimeConfig();

                // 创建编辑器专用 Runtime
                var context = new SetupContext();
                context.Logger = new PuffinLogger();
                context.ScannerConfig = scannerConfig;
                context.runtimeConfig = runtimeConfig;

                _editorRuntime = new GameSystemRuntime(context.Logger);
                _editorRuntime.IsEditorMode = true;
                _editorRuntime.EnableProfiling = runtimeConfig.EnableProfiling;

                // 先设置 PuffinFramework 的编辑器 Runtime 引用，确保 Log 可用
                PuffinFramework.SetEditorRuntime(_editorRuntime, context);

                foreach (var symbol in runtimeConfig.Symbols)
                    _editorRuntime.AddSymbol(symbol);

                // 扫描支持编辑器的系统
                var editorSystemTypes = ScanEditorSupportSystems(scannerConfig);

                // 注册系统（不调用运行时生命周期，只调用 OnRegister 和 OnInitializeAsync）
                await _editorRuntime.CreateSystemFromTypesAsync(editorSystemTypes);

                // 缓存配置用于后续同步
                _cachedScannerConfig = scannerConfig;

                // 调用编辑器初始化
                foreach (var system in _editorRuntime.GetAllSystems())
                {
                    if (system is IEditorSupport editorSupport)
                    {
                        try
                        {
                            editorSupport.OnEditorInitialize();
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void OnRegistrySettingsChanged()
        {
            if (_editorRuntime == null || _cachedScannerConfig == null)
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            SyncEditorSystemsAsync().Forget();
        }

        private static async UniTask SyncEditorSystemsAsync()
        {
            // 获取当前应该注册的系统类型
            var expectedTypes = new HashSet<Type>(ScanEditorSupportSystems(_cachedScannerConfig));

            // 获取当前已注册的系统类型
            var registeredTypes = new HashSet<Type>(_editorRuntime.GetAllSystems().Select(s => s.GetType()));

            // 找出需要取消注册的系统（已注册但不应该存在的）
            var toUnregister = registeredTypes.Except(expectedTypes).ToList();
            foreach (var type in toUnregister)
                _editorRuntime.UnRegister(type);

            // 找出需要注册的系统（应该存在但未注册的）
            var toRegister = expectedTypes.Except(registeredTypes).ToList();
            foreach (var type in toRegister)
            {
                await _editorRuntime.RegisterSystemAsync(type);

                // 调用编辑器初始化
                var system = _editorRuntime.GetAllSystems().FirstOrDefault(s => s.GetType() == type);
                if (system is IEditorSupport editorSupport)
                {
                    try
                    {
                        editorSupport.OnEditorInitialize();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }

        private static Type[] ScanEditorSupportSystems(ScannerConfig config)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => ShouldScanAssembly(a, config))
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch
                    {
                        return Array.Empty<Type>();
                    }
                })
                .Where(t => IsEditorSupportSystem(t, config))
                .ToArray();
        }

        private static bool ShouldScanAssembly(Assembly assembly, ScannerConfig config)
        {
            var name = assembly.GetName().Name;

            if (config.ExcludeAssemblyPrefixes.Any(p => name.StartsWith(p)))
                return false;

            // 检查模块是否被禁用
            var moduleSettings = ModuleRegistrySettings.Instance;
            if (moduleSettings != null && moduleSettings.IsAssemblyDisabled(name))
                return false;

            if (config.AssemblyPrefixes.Count > 0)
                return config.AssemblyPrefixes.Any(p => name.StartsWith(p));

            return true;
        }

        private static bool IsEditorSupportSystem(Type type, ScannerConfig config)
        {
            if (!typeof(IGameSystem).IsAssignableFrom(type))
                return false;
            if (type.IsAbstract || type.IsInterface)
                return false;
            if (!typeof(IEditorSupport).IsAssignableFrom(type))
                return false;

            if (config.RequireAutoRegister && type.GetCustomAttribute<AutoRegisterAttribute>() == null)
                return false;

            // 检查系统是否被禁用
            var systemSettings = SystemRegistrySettings.Instance;
            if (systemSettings != null && systemSettings.IsSystemDisabled(type))
                return false;

            return true;
        }
#endif

        /// <summary>
        /// 安装环境
        /// </summary>
        public virtual void Setup()
        {
            SetupContext context = new SetupContext();
            context.Logger = new PuffinLogger();
            context.ResourcesLoader = new DefaultResourceLoader();
            PuffinFramework.Setup(context);
        }

        /// <summary>
        /// 启动框架,自动在运行开始的时候调用
        /// </summary>
        /// <returns></returns>
        public virtual UniTask StartAsync()
        {
            PuffinFramework.Start();
            return UniTask.CompletedTask;
        }
    }
}