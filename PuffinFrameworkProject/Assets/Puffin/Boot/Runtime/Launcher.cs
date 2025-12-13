using System;
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
using UnityEditor;
using UnityEngine;

namespace Puffin.Boot.Runtime
{
    /// <summary>
    /// 启动器
    /// </summary>
    public class Launcher : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeRuntime()
        {
            var launcher = FindAnyObjectByType<Launcher>();
            if (launcher != null)
            {
                launcher.Setup();
                
                var settings = Puffinettings.Instance;
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
        }

        private static GameSystemRuntime _editorRuntime;

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
                
                var settings = Puffinettings.Instance;
                var scannerConfig = settings.ToScannerConfig();
                var runtimeConfig = settings.ToRuntimeConfig();

                // 创建编辑器专用 Runtime
                var context = new SetupContext();
                context.Logger = new DefaultLogger();
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

            return true;
        }
#endif

        // private void Awake()
        // {
        //     // UnityEngine.Rendering.DebugManager.instance.enableRuntimeUI = false;
        //     DontDestroyOnLoad(gameObject);
        // }

        /// <summary>
        /// 安装环境
        /// </summary>
        public virtual void Setup()
        {
            
            SetupContext context = new SetupContext();
            context.Logger = new DefaultLogger();
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