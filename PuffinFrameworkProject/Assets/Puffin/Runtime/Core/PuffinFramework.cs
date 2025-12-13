using System;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using Puffin.Runtime.Core.Configs;
using Puffin.Runtime.Events.Core;
using Puffin.Runtime.Interfaces;
using Puffin.Runtime.Settings;
using Puffin.Runtime.Tools;
using UnityEditor;
using UnityEngine;
using Application = UnityEngine.Application;

namespace Puffin.Runtime.Core
{
    public static class PuffinFramework
    {
        private const string Version = "0.0.1";

        /// <summary>
        /// 应用程序是否启动
        /// </summary>
        public static bool IsApplicationStarted { get; private set; }

        /// <summary>
        /// 是否初始化
        /// </summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>
        /// 是否初始化中
        /// </summary>
        public static bool IsInitializing { get; private set; }

        /// <summary>
        /// 全局事件
        /// </summary>
        public static EventDispatcher Dispatcher { get; private set; } = new();


        /// <summary>
        /// 日志
        /// </summary>
        public static IPuffinLogger Logger { private set; get; }

        /// <summary>
        /// 资源加载器
        /// </summary>
        public static IResourcesLoader ResourcesLoader { private set; get; }

        /// <summary>
        /// 扫描配置
        /// </summary>
        public static ScannerConfig ScannerConfig { private set; get; }

        /// <summary>
        /// 运行时配置
        /// </summary>
        public static RuntimeConfig RuntimeConfig { private set; get; }

        public static bool IsSetup { private set; get; }

        /// <summary>
        /// Runtime 实例
        /// </summary>
        private static GameSystemRuntime _runtime;

#if UNITY_EDITOR
        private static GameSystemRuntime _editorRuntime;

        /// <summary>
        /// 设置编辑器 Runtime（由 PuffinFrameworkEditorInitializer 调用）
        /// </summary>
        public static void SetEditorRuntime(GameSystemRuntime editorRuntime, SetupContext context)
        {
            _editorRuntime = editorRuntime;
            Setup(context);
        }

        /// <summary>
        /// 获取当前有效的 Runtime（编辑器模式下返回编辑器 Runtime）
        /// </summary>
        public static GameSystemRuntime EffectiveRuntime =>
            EditorApplication.isPlaying ? _runtime : _editorRuntime;
#else
        public static GameSystemRuntime EffectiveRuntime => Runtime;
#endif

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static void PlayingStateChanged()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    Reset();
                }
            };
        }
#endif

        static void Reset()
        {
            Logger = null;
            ResourcesLoader = null;
            ScannerConfig = null;
            RuntimeConfig = null;
            _editorRuntime = null;
            IsApplicationStarted = false;
            IsInitialized = false;
            IsInitializing = false;
            _runtime = null;
            IsSetup = false;
            Dispatcher = new EventDispatcher();
        }

        /// <summary>
        /// 调用框架内任何东西必须先进行安装环境
        /// </summary>
        /// <param name="setupContext"></param>
        public static void Setup(SetupContext setupContext)
        {
            if (IsSetup) throw new Exception("PuffinFramework is already Setup.");

            ResourcesLoader = setupContext.ResourcesLoader ?? new DefaultResourceLoader();
            Logger = setupContext.Logger ?? new DefaultLogger();
            ScannerConfig = setupContext.ScannerConfig ?? Puffinettings.Instance.ToScannerConfig();
            RuntimeConfig = setupContext.runtimeConfig ?? Puffinettings.Instance.ToRuntimeConfig();
            IsSetup = true;
            Logger.Info("Puffin Framework Setup!");
        }

        static void ThrowIfNotSetup()
        {
            if (!IsSetup) throw new Exception("PuffinFramework is not Setup,Please call Setup() method.");
        }

        /// <summary>
        /// 启动框架
        /// </summary>
        public static void Start()
        {
            ThrowIfNotSetup();

            StartAsync().Forget();
        }

        static async UniTask StartAsync()
        {
            if (IsInitialized || IsInitializing)
                return;

            IsInitializing = true;

            LogSystemInfo();

            var totalWatch = new Stopwatch();

            IsApplicationStarted = true;

            // 创建扫描器
            var scanner = new GameSystemScanner(Logger, ScannerConfig);


            var scannedTypes = await scanner.ScanAsync();

            // 合并手动指定的类型
            if (RuntimeConfig.ManualSystemTypes.Count > 0)
            {
                var allTypes = new Type[scannedTypes.Length + RuntimeConfig.ManualSystemTypes.Count];
                scannedTypes.CopyTo(allTypes, 0);
                RuntimeConfig.ManualSystemTypes.CopyTo(allTypes, scannedTypes.Length);
                scannedTypes = allTypes;
            }

            _runtime = new GameSystemRuntime(PuffinFramework.Logger);
            _runtime.EnableProfiling = RuntimeConfig.EnableProfiling;

            // 添加条件符号
            foreach (var symbol in RuntimeConfig.Symbols)
                _runtime.AddSymbol(symbol);

            // 创建 RuntimeBehaviour
            CreateRuntimeBehaviour();

            // 注册系统
            await _runtime.CreateSystemFromTypesAsync(scannedTypes);


            // 发送初始化完成事件
            Dispatcher.SendDefault<SystemEventDefines.OnGameInitialized>();

            IsInitializing = false;
            IsInitialized = true;

            PuffinFramework.Logger.Info($"框架启动完成，总耗时: {totalWatch.Elapsed.TotalSeconds}s");
        }

        /// <summary>
        /// 获取系统
        /// </summary>
        public static T GetSystem<T>() where T : class, IGameSystem
        {
            if (EffectiveRuntime == null)
                throw new Exception("PuffinFramework 未初始化");
            return EffectiveRuntime.GetSystem<T>();
        }

        /// <summary>
        /// 检查系统是否存在
        /// </summary>
        public static bool HasSystem<T>() where T : class, IGameSystem
        {
            return EffectiveRuntime?.HasSystem<T>() ?? false;
        }

        /// <summary>
        /// 暂停 Runtime
        /// </summary>
        public static void Pause() => _runtime?.Pause();

        /// <summary>
        /// 恢复 Runtime
        /// </summary>
        public static void Resume() => _runtime?.Resume();

        private static void CreateRuntimeBehaviour()
        {
            var go = new GameObject("[PuffinFramework Runtime]");
            var behaviour = go.AddComponent<PuffinFrameworkRuntimeBehaviour>();
            behaviour.Initialize(_runtime);
        }

        private static void LogSystemInfo()
        {
            var settings = Puffinettings.Instance;
            var level = settings?.systemInfoLevel ?? SystemInfoLevel.Simple;

            if (level == SystemInfoLevel.None)
                return;

            Logger.Separator("PuffinFramework");

            // 基础信息（Simple 和 Detailed 都输出）
#if UNITY_EDITOR
            Logger.Info($"游戏: {PlayerSettings.productName}");
#endif
            Logger.Info($"PuffinFramework框架版本: {Version}");
            Logger.Info($"平台: {Application.platform}");
            Logger.Info($"屏幕分辨率: {Screen.width} x {Screen.height}");

            if (level == SystemInfoLevel.Simple)
                return;

            // 详细信息（仅 Detailed 输出）
            Logger.Separator("系统详细信息");

            // Unity 信息
            Logger.Info($"Unity 版本: {Application.unityVersion}");
            Logger.Info($"系统语言: {Application.systemLanguage}");
            Logger.Info($"时间: {DateTime.Now:U}");

            // 设备信息
            Logger.Info($"设备名称: {SystemInfo.deviceName}");
            Logger.Info($"设备型号: {SystemInfo.deviceModel}");
            Logger.Info($"操作系统: {SystemInfo.operatingSystem}");

            // 处理器信息
            Logger.Info($"处理器: {SystemInfo.processorType}");
            Logger.Info($"处理器核心数: {SystemInfo.processorCount}");

            // 内存信息
            Logger.Info($"系统内存: {SystemInfo.systemMemorySize} MB");
            Logger.Info($"显存: {SystemInfo.graphicsMemorySize} MB");

            // 显卡信息
            Logger.Info($"显卡: {SystemInfo.graphicsDeviceName}");
            Logger.Info($"显卡类型: {SystemInfo.graphicsDeviceType}");

            // 应用信息
            Logger.Info($"应用版本: {Application.version}");
            Logger.Info($"数据路径: {Application.dataPath}");
            Logger.Info($"持久化路径: {Application.persistentDataPath}");
        }
    }
}