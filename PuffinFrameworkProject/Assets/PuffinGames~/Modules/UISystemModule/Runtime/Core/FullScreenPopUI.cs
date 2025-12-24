namespace XFrameworks.Systems.UISystems.Core
{
    /// <summary>
    /// 全屏弹窗
    /// 阻断游戏输入，切换到UI输入模式
    /// </summary>
    public class FullScreenPopUI : PopUI
    {
        public override int layer => UILayerDefines.FullScreenPopBase;

        /// <summary>
        /// 是否阻断游戏输入
        /// </summary>
        protected virtual bool blockGameInput => true;

        protected override void OnShow()
        {
            base.OnShow();
            if (blockGameInput)
            {
                // TODO: 切换到UI输入模式
                // InputSystem.SetInputMode(InputMode.UI);
            }
        }

        protected override void OnHide()
        {
            base.OnHide();
            if (blockGameInput)
            {
                // TODO: 恢复游戏输入模式
                // InputSystem.SetInputMode(InputMode.Game);
            }
        }
    }
}
