namespace XFrameworks.Systems.UISystems.Core
{
    /// <summary>
    /// 主UI基类
    /// 同一时间只有一个MainUI处于活动状态
    /// 切换MainUI时会自动处理附加的UI（根据各UI的mainUIChangeBehavior）
    /// </summary>
    public class MainUI : FocusUI
    {
        public override int layer => UILayerDefines.MainUIBase;

        public override bool useMask => false;

        public override MainUIChangeBehavior mainUIChangeBehavior => MainUIChangeBehavior.None;
    }
}
