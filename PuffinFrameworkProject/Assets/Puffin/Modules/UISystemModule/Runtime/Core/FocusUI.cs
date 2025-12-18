using XFrameworks.Systems.UISystems.Interface;

namespace XFrameworks.Systems.UISystems.Core
{
    public class FocusUI : Panel, IUIFocus
    {
        private bool _focus;

        public bool isFocused
        {
            get => _focus;
            set
            {
                if (_focus != value)
                {
                    _focus = value;
                    OnFocusChanged();
                }
            }
        }

        protected virtual void OnFocusChanged()
        {
            // Log.Info(GetType().Name + " Focus改变:" + Focus);
        }
    }
}