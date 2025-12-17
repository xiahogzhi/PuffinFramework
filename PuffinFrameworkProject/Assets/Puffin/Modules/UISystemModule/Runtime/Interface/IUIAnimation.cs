using System;
using XFrameworks.Systems.UISystems.Core;

namespace XFrameworks.Systems.UISystems.Interface
{
    /// <summary>
    /// UI动画接口
    /// </summary>
    public interface IUIAnimation
    {
        /// <summary>
        /// 播放动画
        /// </summary>
        /// <param name="panel">目标Panel</param>
        /// <param name="finishAction">完成回调</param>
        void OnPlaying(Panel panel, Action finishAction);

        /// <summary>
        /// 停止动画
        /// </summary>
        void Kill();
    }
}
