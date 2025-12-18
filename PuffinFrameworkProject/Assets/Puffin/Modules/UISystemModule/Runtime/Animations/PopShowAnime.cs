#if DOTWEEN
using System;
using DG.Tweening;
using UnityEngine;
using XFrameworks.Systems.UISystems.Core;
using XFrameworks.Systems.UISystems.Interface;

namespace XFrameworks.Systems.UISystems.Animations
{
    /// <summary>
    /// 弹窗显示动画 - 缩放弹出效果
    /// </summary>
    public class PopShowAnime : IUIAnimation
    {
        private Tweener _curTween;

        public void OnPlaying(Panel panel, Action finishAction)
        {
            panel.transform.localScale = Vector3.zero;
            _curTween = panel.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack).SetUpdate(true);
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
