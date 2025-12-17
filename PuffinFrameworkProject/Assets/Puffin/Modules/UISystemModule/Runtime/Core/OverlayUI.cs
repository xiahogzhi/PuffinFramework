namespace XFrameworks.Systems.UISystems.Core
{
    /// <summary>
    /// 叠加UI
    /// 无特殊操作，始终显示在最上层
    /// 不受MainUI切换影响
    /// 适用于：Loading、Toast、Debug等
    /// </summary>
    public class OverlayUI : Panel
    {
        public override int layer => UILayerDefines.OverlayBase;

        public override bool useMask => false;

        public override MainUIChangeBehavior mainUIChangeBehavior => MainUIChangeBehavior.None;
    }
}
