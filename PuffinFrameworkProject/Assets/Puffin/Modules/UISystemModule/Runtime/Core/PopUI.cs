using System;
using UnityEngine.UI;
using XFrameworks.Systems.UISystems.Interface;

namespace XFrameworks.Systems.UISystems.Core
{
    public enum AutoCloseTopTypeEnum
    {
        None,
        Ignore,
        Hide,
        Close,
    }

    /// <summary>
    /// 弹窗基类
    /// MainUI切换时默认关闭
    /// </summary>
    public class PopUI : FocusUI, IMaskClickable
    {
        public enum MaskClickOperationType
        {
            None,
            Hide,
            Close,
            DirectHide,
            DirectClose,
        }

        protected virtual MaskClickOperationType maskClickOperation => MaskClickOperationType.None;

        public override MainUIChangeBehavior mainUIChangeBehavior => MainUIChangeBehavior.Close;


        public Guid token { get; } = Guid.NewGuid();

        protected virtual bool autoPauseGame => true;


        /// <summary>
        /// 是否可以自动关闭,右键 ECS会进行自动关闭
        /// </summary>
        public virtual AutoCloseTopTypeEnum autoCloseTopType => AutoCloseTopTypeEnum.Hide;

        protected virtual bool useUIEvent { get; } = true;

        protected override void OnCreate()
        {
            base.OnCreate();
            var btn = transform.Find("CloseBtn");
            if (btn)
            {
                btn.GetComponent<Button>().onClick.AddListener(() =>
                {
                    // Core.UISystem.instance.AutoClosePopUI(this, true);
                });
            }
        }

        protected override void OnShow()
        {
            base.OnShow();
            // if (autoPauseGame)
            //     XFramework.Pause(token,  200);
        }

        protected override void OnHide()
        {
            base.OnHide();
            // if (autoPauseGame) XFramework.Resume(token);
        }

        protected override void OnScriptActivate()
        {
            base.OnScriptActivate();
            // if (useUIEvent)
            //     InputSystemModule.instance.SetMap(token, InputMapTypeEnum.UI, 200);
        }

        protected override void OnScriptDeactivate()
        {
            base.OnScriptDeactivate();
            // if (useUIEvent)
            //     InputSystemModule.instance.RemoveMap(token);
        }


        public virtual void OnMaskClick()
        {
            switch (maskClickOperation)
            {
                case MaskClickOperationType.None:
                    break;
                case MaskClickOperationType.Hide:
                    Hide();
                    break;
                case MaskClickOperationType.Close:
                    Close();
                    break;
                case MaskClickOperationType.DirectClose:
                    Close(false);
                    break;
                case MaskClickOperationType.DirectHide:
                    Hide(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}