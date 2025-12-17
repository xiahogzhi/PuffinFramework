namespace XFrameworks.Systems.UISystems.Core
{
    /// <summary>
    /// UI层级定义
    /// </summary>
    public static class UILayerDefines
    {
        // MainUI Layer: 0-99
        public const int MainUIBase = 0;
        public const int MainUIMax = 99;

        // AdditiveUI Layer: 100-199
        public const int AdditiveUIBase = 100;
        public const int AdditiveUIMax = 199;

        // FloatingPopUI Layer: 200-299
        public const int FloatingPopBase = 200;
        public const int FloatingPopMax = 299;

        // FullScreenPopUI Layer: 300-499
        public const int FullScreenPopBase = 300;
        public const int FullScreenPopMax = 499;

        // OverlayUI Layer: 500-999
        public const int OverlayBase = 500;
        public const int ToastLayer = 600;
        public const int LoadingLayer = 700;
        public const int DebugLayer = 900;
        public const int OverlayMax = 999;
    }
}
