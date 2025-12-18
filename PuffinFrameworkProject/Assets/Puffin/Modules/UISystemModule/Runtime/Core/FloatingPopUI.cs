namespace XFrameworks.Systems.UISystems.Core
{
    /// <summary>
    /// 浮动弹窗
    /// 不阻断游戏输入，类似提示类UI
    /// </summary>
    public class FloatingPopUI : PopUI
    {
        public override int layer => UILayerDefines.FloatingPopBase;

        public override bool useMask => false;

        /// <summary>
        /// 是否阻断游戏输入
        /// </summary>
        protected virtual bool blockGameInput => false;
    }
}
