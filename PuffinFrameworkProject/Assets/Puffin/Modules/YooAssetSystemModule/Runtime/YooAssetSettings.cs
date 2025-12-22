#if YOOASSET_INSTALLED
using Puffin.Runtime.Settings;
using UnityEngine;
using YooAsset;

namespace YooAssetSystemModule.Runtime
{
    /// <summary>
    /// YooAsset系统配置
    /// </summary>
    [PuffinSetting("YooAsset配置")]
    public class YooAssetSettings : SettingsBase<YooAssetSettings>
    {
        [Header("运行模式")]
        [Tooltip("编辑器模拟模式：使用AssetDatabase加载\n离线模式：使用本地资源包\n联机模式：支持热更新")]
        public EPlayMode playMode = EPlayMode.EditorSimulateMode;

        [Header("包配置")]
        [Tooltip("默认资源包名称")]
        public string defaultPackageName = "DefaultPackage";

        [Header("远程配置")]
        [Tooltip("资源服务器地址")]
        public string hostServerURL = "http://127.0.0.1";

        [Tooltip("备用资源服务器地址")]
        public string fallbackHostServerURL = "";

        [Header("下载配置")]
        [Tooltip("同时下载的最大文件数")]
        public int downloadingMaxNum = 10;

        [Tooltip("失败重试最大次数")]
        public int failedTryAgain = 3;

        [Header("缓存配置")]
        [Tooltip("验证级别：Low-快速验证 High-完整验证")]
        public EVerifyLevel verifyLevel = EVerifyLevel.Middle;
    }
}
#endif
