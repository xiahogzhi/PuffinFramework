namespace XFrameworks.Systems.UISystems.Core
{
    /// <summary>
    /// 叠加UI（无遮罩）
    /// MainUI切换时默认隐藏
    /// </summary>
    public class AdditiveUI : Panel
    {
        public override int layer => UILayerDefines.AdditiveUIBase;

        public override bool useMask => false;

        public override MainUIChangeBehavior mainUIChangeBehavior => MainUIChangeBehavior.Hide;
    }
} 