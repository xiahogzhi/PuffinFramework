namespace XFrameworks.Systems.UISystems.Core
{
    /// <summary>
    /// MainUI切换时的行为
    /// </summary>
    public enum MainUIChangeBehavior
    {
        /// <summary>
        /// 不处理（OverlayUI默认）
        /// </summary>
        None,

        /// <summary>
        /// 隐藏但保留实例（AdditiveUI默认）
        /// </summary>
        Hide,

        /// <summary>
        /// 关闭并销毁（PopUI默认）
        /// </summary>
        Close,
    }
}
