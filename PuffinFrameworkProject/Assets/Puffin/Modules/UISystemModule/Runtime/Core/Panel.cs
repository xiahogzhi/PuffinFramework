using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Puffin.Modules.GameDevKit.Runtime.Behaviours;
using Puffin.Runtime.Core.Attributes;
using UnityEngine;
using XFrameworks.Runtime.Core;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
#if DOTWEEN
using DG.Tweening;
#endif

namespace XFrameworks.Systems.UISystems.Core
{
    public class Panel : GameScript
    {
        /// <summary>
        /// 自动注入
        /// </summary>
        [Inject]
        public UISystem UISystem { set; get; }

#if ODIN_INSPECTOR
        [LabelText("层级")]
#endif
        [SerializeField] private int _layer = 10;

        private enum AnimationTypeEnum
        {
            None,
            Animator,
            CanvasGroup,
#if DOTWEEN
            DoTweenPop,
#endif
        }

#if ODIN_INSPECTOR
        [LabelText("动画类型")]
#endif
        [SerializeField] private AnimationTypeEnum _animationType = AnimationTypeEnum.None;

#if DOTWEEN
#if ODIN_INSPECTOR
        [LabelText("动画时长")]
        [ShowIf("@_animationType != AnimationTypeEnum.None && _animationType != AnimationTypeEnum.Animator")]
#endif
        [SerializeField] private float _animationDuration = 0.2f;
#endif

        /// <summary>
        /// 使用Mask
        /// </summary>
        public virtual bool useMask => true;

        /// <summary>
        /// 当前Panel的路径,创建后初始化赋予
        /// </summary>
        public string path { private set; get; }

        public bool isAnimationPlaying { protected set; get; }

        public object userData { set; get; }

        /// <summary>
        /// 层级
        /// </summary>
        public virtual int layer => _layer;

        /// <summary>
        /// MainUI切换时的行为
        /// </summary>
        public virtual MainUIChangeBehavior mainUIChangeBehavior => MainUIChangeBehavior.Close;

        private List<View> _views = new List<View>();

        public void Initialize(string path)
        {
            this.path = path;
            try
            {
                OnCreate();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            try
            {
                foreach (var view in _views)
                    view.OnCreate();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private StateEnum _state = StateEnum.Hidden;

        [Flags]
        public enum StateEnum
        {
            None = 0,
            Show = 1 << 1,
            Shown = 1 << 2,
            Hide = 1 << 3,
            Hidden = 1 << 4,
            Close = 1 << 5,
        }

        protected void SetState(StateEnum state)
        {
            if (_state != state)
            {
                var last = _state;
                _state = state;
                OnStateChanged(last, state);
            }
        }

        public bool IsState(StateEnum state)
        {
            return (_state & state) > 0;
        }

        protected virtual void OnStateChanged(StateEnum last, StateEnum cur)
        {
            switch (cur)
            {
                case StateEnum.None:
                    break;
                case StateEnum.Show:
                {
                    try
                    {
                        OnShow();
                        foreach (var view in _views) view.OnShow();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
                    break;
                case StateEnum.Shown:
                {
                    try
                    {
                        OnShown();
                        foreach (var view in _views) view.OnShown();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
                    break;
                case StateEnum.Hide:
                {
                    try
                    {
                        OnHide();
                        foreach (var view in _views) view.OnHide();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
                    break;
                case StateEnum.Hidden:
                {
                    try
                    {
                        OnHidden();
                        foreach (var view in _views) view.OnHidden();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
                    break;
                case StateEnum.Close:
                    try
                    {
                        OnClose();
                        foreach (var view in _views) view.OnClose();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cur), cur, null);
            }
        }

        protected virtual void OnClose()
        {
        }

        public virtual void Hide(bool useAnimation = true)
        {
            if (!IsState(StateEnum.Shown))
                return;
            HideAsync(useAnimation).Forget();
        }

        public virtual async UniTask HideAsync(bool useAnimation = true)
        {
            if (!IsState(StateEnum.Shown))
                return;

            if (!useAnimation)
            {
                SetState(StateEnum.Hide);
                gameObject.SetActive(false);
                SetState(StateEnum.Hidden);
                UISystem.RefreshUI();
                return;
            }

            SetState(StateEnum.Hide);
            UISystem.RefreshUI();

            await HideAnimationAsync();
            gameObject.SetActive(false);

            SetState(StateEnum.Hidden);
        }

        public async UniTask WaitEnd()
        {
            var cancel = gameObject.GetCancellationTokenOnDestroy();
            while (IsState(StateEnum.Show | StateEnum.Shown))
            {
                var flag = await UniTask.Yield(cancellationToken: cancel).SuppressCancellationThrow();
                if (flag)
                    return;
            }
        }

        public virtual void Show(bool useAnimation = true)
        {
            if (!IsState(StateEnum.Hidden))
                return;
            ShowAsync(useAnimation).Forget();
        }

        public virtual async UniTask ShowAsync(bool useAnimation = true)
        {
            if (!IsState(StateEnum.Hidden))
                return;
            if (!useAnimation)
            {
                SetState(StateEnum.Show);
                gameObject.SetActive(true);
                SetState(StateEnum.Shown);
                UISystem.RefreshUI();
                return;
            }

            SetState(StateEnum.Show);
            gameObject.SetActive(true);

            UISystem.RefreshUI();

            await ShowAnimationAsync();

            SetState(StateEnum.Shown);
        }

        public virtual void Close(bool useAnimation = true)
        {
            CloseAsync(useAnimation).Forget();
        }

        public virtual async UniTask CloseAsync(bool useAnimation = true)
        {
            if (!useAnimation)
            {
                SetState(StateEnum.Hide);
                OnHide();
                gameObject.SetActive(false);
                SetState(StateEnum.Hidden);
                OnHidden();
                SetState(StateEnum.Close);
                UISystem.RefreshUI();
                UISystem.Destroy(this);
                return;
            }

            if (!IsState(StateEnum.Hidden | StateEnum.Shown | StateEnum.Close))
                return;

            SetState(StateEnum.Hide);
            UISystem.RefreshUI();

            await HideAnimationAsync();
            gameObject.SetActive(false);

            SetState(StateEnum.Hidden);

            SetState(StateEnum.Close);

            UISystem.Destroy(this);
        }

        protected virtual async UniTask HideAnimationAsync()
        {
            if (_animationType == AnimationTypeEnum.None)
                return;

            isAnimationPlaying = true;

            try
            {
                switch (_animationType)
                {
                    case AnimationTypeEnum.Animator:
                        await PlayAnimatorHide();
                        break;
                    case AnimationTypeEnum.CanvasGroup:
                        await PlayCanvasGroupHide();
                        break;
#if DOTWEEN
                    case AnimationTypeEnum.DoTweenPop:
                        await PlayDoTweenHide();
                        break;
#endif
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            isAnimationPlaying = false;
        }

        protected virtual async UniTask ShowAnimationAsync()
        {
            if (_animationType == AnimationTypeEnum.None)
                return;

            isAnimationPlaying = true;

            try
            {
                switch (_animationType)
                {
                    case AnimationTypeEnum.Animator:
                        await PlayAnimatorShow();
                        break;
                    case AnimationTypeEnum.CanvasGroup:
                        await PlayCanvasGroupShow();
                        break;
#if DOTWEEN
                    case AnimationTypeEnum.DoTweenPop:
                        await PlayDoTweenShow();
                        break;
#endif
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            isAnimationPlaying = false;
        }

        private async UniTask PlayAnimatorShow()
        {
            var animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.Play("show");
                await UniTask.Yield();
                var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                await UniTask.WaitForSeconds(stateInfo.length, true);
            }
        }

        private async UniTask PlayAnimatorHide()
        {
            var animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.Play("hide");
                await UniTask.Yield();
                var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                await UniTask.WaitForSeconds(stateInfo.length, true);
            }
        }

        private async UniTask PlayCanvasGroupShow()
        {
            var group = GetOrAddCanvasGroup();
            group.alpha = 0;
            float elapsed = 0;
            float duration = 0.2f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(elapsed / duration);
                await UniTask.Yield();
            }
            group.alpha = 1;
        }

        private async UniTask PlayCanvasGroupHide()
        {
            var group = GetOrAddCanvasGroup();
            group.alpha = 1;
            float elapsed = 0;
            float duration = 0.1f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = 1 - Mathf.Clamp01(elapsed / duration);
                await UniTask.Yield();
            }
            group.alpha = 0;
        }

#if DOTWEEN
        private async UniTask PlayDoTweenShow()
        {
            var group = GetOrAddCanvasGroup();
            group.alpha = 0;
            transform.localScale = Vector3.one * 0.5f;

            transform.DOScale(Vector3.one, _animationDuration).SetEase(Ease.OutBack).SetUpdate(true);
            //group.DOFade(1, _animationDuration).SetUpdate(true);
            

            await UniTask.WaitForSeconds(_animationDuration, true);
        }

        private async UniTask PlayDoTweenHide()
        {
            var group = GetOrAddCanvasGroup();

            transform.DOScale(Vector3.one * 0.5f, _animationDuration).SetEase(Ease.InBack).SetUpdate(true);
            // group.DOFade(0, _animationDuration).SetUpdate(true);

            await UniTask.WaitForSeconds(_animationDuration, true);
        }
#endif

        private CanvasGroup GetOrAddCanvasGroup()
        {
            var group = GetComponent<CanvasGroup>();
            if (group == null)
                group = gameObject.AddComponent<CanvasGroup>();
            return group;
        }

        protected virtual void OnHide()
        {
        }

        protected virtual void OnHidden()
        {
        }

        protected virtual void OnShow()
        {
        }

        protected virtual void OnShown()
        {
        }

        protected virtual void OnCreate()
        {
        }

        public void RegisterView(View view)
        {
            _views.Add(view);
        }
    }
}
