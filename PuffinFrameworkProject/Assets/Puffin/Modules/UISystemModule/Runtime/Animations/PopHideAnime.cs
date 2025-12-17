#if DOTWEEN
using System;
using DG.Tweening;
using UnityEngine;
using XFrameworks.Systems.UISystems.Core;
using XFrameworks.Systems.UISystems.Interface;

namespace XFrameworks.Systems.UISystems.Animations
{
    /// <summary>
    /// 弹窗隐藏动画 - 缩放收缩效果
    /// </summary>
    public class PopHideAnime : IUIAnimation
    {
        private Tweener _curTween;

        public void OnPlaying(Panel panel, Action finishAction)
        {
            panel.transform.localScale = Vector3.one;
            _curTween = panel.transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack).SetUpdate(true);
            _curTween.OnComplete(() =>
            {
                _curTween = null;
                finishAction?.Invoke();
            });
        }

        public void Kill()
        {
            if (_curTween != null)
            {
                _curTween.Kill();
                _curTween = null;
            }
        }
    }
}
#endif
