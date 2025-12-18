using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Puffin.Runtime.Core;
using Puffin.Runtime.Core.Attributes;
using Puffin.Runtime.Interfaces;
using Puffin.Runtime.Interfaces.SystemEvents;
using Puffin.Runtime.Tools;
using UnityEngine;
using UnityEngine.UI;
using XFrameworks.Runtime.Core;
using XFrameworks.Systems.UISystems.Interface;
using XFrameworks.Utils;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace XFrameworks.Systems.UISystems.Core
{
    [SystemAlias("UI系统")]
    public class UISystem : IGameSystem, IInitializeAsync
    {
        /// <summary>
        /// Resources下的目录路径
        /// </summary>
        public const string DefaultFolder = "UI";

        /// <summary>
        /// 实例化的所有UI
        /// </summary>
        private readonly List<Panel> _instanceUIs = new List<Panel>();

        /// <summary>
        /// 所有层级
        /// </summary>
        private readonly Dictionary<int, Transform> _layers = new Dictionary<int, Transform>();

        /// <summary>
        /// 默认Mask
        /// </summary>
        private Transform _mask;

        // private GameObject _blurMask;

        /// <summary>
        /// 当前焦点UI,每次更新会选择优先级最高的UI
        /// </summary>
        private IUIFocus _currentUIFocus;

        /// <summary>
        /// 持久化UI
        /// </summary>
        private readonly HashSet<Panel> _persistenceUI = new HashSet<Panel>();

        /// <summary>
        /// 所有加载的Panel,如果在某个目录下,则需在Panel上面配置指定目录
        /// Panel必须放置在Resources/UI目录下
        /// </summary>
        private readonly Dictionary<string, Panel> _loadedUI = new Dictionary<string, Panel>();

        /// <summary>
        /// 当前MainUI
        /// </summary>
        private MainUI _currentMainUI;

        /// <summary>
        /// MainUI历史栈
        /// </summary>
        private readonly Stack<MainUI> _mainUIHistory = new Stack<MainUI>();

        /// <summary>
        /// 获取当前MainUI
        /// </summary>
        public MainUI CurrentMainUI => _currentMainUI;


        public T FindUI<T>() where T : Panel
        {
            return FindUI(GetPath(typeof(T))) as T;
        }

        public Panel FindUI(string path)
        {
            foreach (var variable in _instanceUIs)
            {
                if (variable.path == path)
                    return variable;
            }

            return null;
        }


        private Transform GetLayer(int layer)
        {
            if (_layers.TryGetValue(layer, out var p))
                return p;

            return CreateLayer(layer);
        }

        private Transform CreateLayer(int layer)
        {
#if UNITY_EDITOR
            if (!PuffinFramework.IsApplicationStarted) return null;
#endif
            GameObject go = new GameObject("Layer - " + layer, typeof(Canvas), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            go.layer = 5;
            RectTransform trans = (RectTransform) go.transform;
            //todo
            // trans.SetParent(LauncherSetting.instance.systemConfig.uiRoot);
            trans.localScale = Vector3.one;
            trans.localPosition = Vector3.zero;
            trans.anchorMax = Vector2.one;
            trans.anchorMin = Vector2.zero;
            trans.offsetMax = Vector2.zero;
            trans.offsetMin = Vector2.zero;

            canvas.overrideSorting = true;
            canvas.sortingOrder = layer;
            _layers.Add(layer, trans);
            return trans;
        }


        private void SortUI()
        {
            _instanceUIs.Sort((x, y) =>
            {
                if (x.IsState(Panel.StateEnum.Shown | Panel.StateEnum.Show) &&
                    !y.IsState(Panel.StateEnum.Show | Panel.StateEnum.Shown))
                    return -1;

                if (y.IsState(Panel.StateEnum.Shown | Panel.StateEnum.Show) &&
                    !x.IsState(Panel.StateEnum.Shown | Panel.StateEnum.Show))
                    return 1;

                if (y.layer != x.layer)
                    return y.layer.CompareTo(x.layer);

                return y.transform.GetSiblingIndex().CompareTo(x.transform.GetSiblingIndex());
            });
        }

        private void OnMaskClick()
        {
            foreach (var ui in _instanceUIs)
            {
                if (ui.useMask && ui.IsState(Panel.StateEnum.Shown))
                {
                    if (ui is IMaskClickable mask)
                        mask.OnMaskClick();
                    break;
                }
            }
        }

        private Material _blurMat;

        Material blurMat
        {
            get
            {
                if (_blurMat == null)
                {
                    _blurMat = PuffinFramework.ResourcesLoader.Load<Material>(
                        "Assets/Unified-Universal-Blur-0.4.0/Materials/UniversalBlurForUI.mat");
                }

                return _blurMat;
            }
        }

        // public bool hasBlurMask => _blurMask.activeSelf;
        //
        // void InitBlurMask()
        // {
        //     GameObject go = new GameObject("BlurMask", typeof(Image), typeof(Button));
        //     go.layer = 5;
        //     RectTransform rt = (RectTransform)go.transform;
        //     rt.SetParent(LauncherSetting.instance.systemConfig.uiRoot);
        //     rt.anchorMax = Vector2.one;
        //     rt.anchorMin = Vector2.zero;
        //     rt.offsetMax = Vector2.zero;
        //     rt.offsetMin = Vector2.zero;
        //     rt.offsetMax = Vector2.zero;
        //     rt.offsetMin = Vector2.zero;
        //     rt.anchoredPosition3D = Vector3.zero;
        //     rt.localScale = Vector3.one;
        //     rt.transform.SetAsFirstSibling();
        //     Image img = go.GetComponent<Image>();
        //     Button btn = go.GetComponent<Button>();
        //     btn.onClick.AddListener(OnMaskClick);
        //     img.color = new Color(0.72f, 0.72f, 0.72f, 1);
        //     img.material = blurMat;
        //
        //     btn.navigation = new Navigation() { mode = Navigation.Mode.None };
        //     btn.transition = Selectable.Transition.None;
        //     _blurMask = rt.gameObject;
        //     go.SetActive(false);
        // }

        void InitMask()
        {
            GameObject go = new GameObject("Mask", typeof(Image), typeof(Button));
            go.layer = 5;
            RectTransform rt = (RectTransform) go.transform;
            //todo
            // rt.SetParent(LauncherSetting.instance.systemConfig.uiRoot);
            rt.anchorMax = Vector2.one;
            rt.anchorMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.anchoredPosition3D = Vector3.zero;
            rt.localScale = Vector3.one;
            Image img = go.GetComponent<Image>();
            Button btn = go.GetComponent<Button>();
            btn.onClick.AddListener(OnMaskClick);
            img.color = new Color(0, 0, 0, 0.95f);
            // img.material = blurMat;
            // img.material = Resources.Load<Material>("DefaultAssets/Blur Mask");

            btn.navigation = new Navigation() {mode = Navigation.Mode.None};
            btn.transition = Selectable.Transition.None;
            _mask = rt;
            go.SetActive(false);
        }


        public void RefreshUI()
        {
#if UNITY_EDITOR

            if (!PuffinFramework.IsApplicationStarted) return;
#endif
            SortUI();
            bool maskFlag = false;
            bool focusFlag = false;
            // bool useBlur = false;

            //todo
            // _mask.SetParent(LauncherSetting.instance.systemConfig.uiRoot);
            foreach (var ui in _instanceUIs)
            {
                if (!ui.IsState(Panel.StateEnum.Shown | Panel.StateEnum.Show))
                    continue;

                if (!maskFlag && ui.useMask)
                {
                    // useBlur = ui.useBlur;
                    Transform tr = ui.transform;
                    int index = tr.GetSiblingIndex();
                    _mask.SetParent(tr.parent);
                    _mask.SetSiblingIndex(index);
                    _mask.gameObject.SetActive(true);
                    maskFlag = true;
                }

                if (!focusFlag)
                {
                    if (ui is IUIFocus focus)
                    {
                        if (_currentUIFocus != focus)
                        {
                            if (_currentUIFocus != null)
                            {
                                _currentUIFocus.isFocused = false;
                            }

                            _currentUIFocus = focus;
                            _currentUIFocus.isFocused = true;
                        }

                        focusFlag = true;
                    }
                }

                if (maskFlag && focusFlag)
                {
                    break;
                }
            }

            if (!focusFlag && _currentUIFocus != null)
            {
                _currentUIFocus.isFocused = false;
                _currentUIFocus = null;
            }

            // _blurMask.SetActive(useBlur);

            if (!maskFlag)
            {
                _mask.gameObject.SetActive(false);
            }

            // GameFramework.globalDispatcher.SendDefault<UIEventDefines.OnBlurMaskChanged>();
        }

        private Panel Instantiate(string path)
        {
            var ui = LoadUI(path);
            if (ui == null)
                return null;

            Transform layer = GetLayer(ui.layer);

            var go = PrefabBuilder.Get().SetDefaultActive(false).SetPrefab(ui.gameObject).SetParent(layer).Build();

            if (go == null)
                return null;
            ui = go.GetComponent<Panel>();

            go.name = path;

            _instanceUIs.Add(ui);

            // 注入依赖（Panel及其所有子View）
            InjectPanel(ui);

            ui.Initialize(path);

            return ui;
        }

        /// <summary>
        /// 向Panel及其子组件注入依赖
        /// </summary>
        private void InjectPanel(Panel panel)
        {
            // 注入Panel本身
            PuffinFramework.InjectTo(panel);

            // 注入所有子View
            var views = panel.GetComponentsInChildren<View>(true);
            foreach (var view in views)
            {
                PuffinFramework.InjectTo(view);
            }
        }

        public Panel LoadPersistenceUI(string name)
        {
            Panel panel = FindUI(name);

            if (panel == null)
            {
                panel = Instantiate(name);
                if (panel == null)
                    return null;
                panel.gameObject.SetActive(false);
                SetPersistenceUI(panel);
            }

            return panel;
        }

        /// <summary>
        /// 设置持久化UI
        /// </summary>
        /// <param name="uinelUI"></param>
        public void SetPersistenceUI(Panel panel)
        {
            if (panel == null)
                return;
            if (_persistenceUI.Contains(panel))
                return;
            _persistenceUI.Add(panel);
        }

        /// <summary>
        /// 取消持久化UI
        /// </summary>
        /// <param name="uinelUI"></param>
        public void CancelPersistenceUI(Panel panel)
        {
            if (panel == null)
                return;

            if (!_persistenceUI.Contains(panel))
                return;

            _persistenceUI.Remove(panel);
        }


        public void DestroyAll(bool force = false)
        {
            if (force)
                _persistenceUI.Clear();

            var list = new List<Panel>();
            foreach (var ui in _instanceUIs)
            {
                if (_persistenceUI.Contains(ui))
                    continue;
                list.Add(ui);
            }

            foreach (var ui in list)
            {
                _instanceUIs.Remove(ui);
                if (ui.IsState(Panel.StateEnum.Show | Panel.StateEnum.Shown))
                {
                    try
                    {
                        ui.Hide(false);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                Object.Destroy(ui.gameObject);
            }

            RefreshUI();
        }

        /// <summary>
        /// 加载持久化UI,用此方法加载的UI不会被销毁DestroyAll销毁
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T LoadPersistenceUI<T>() where T : Panel
        {
            return (T) LoadPersistenceUI(GetPath(typeof(T)));
        }

        public Panel Show(string path, bool useAnimation = true, object userData = null)
        {
            var ui = FindUI(path);

            if (ui == null)
                ui = Instantiate(path);

            if (ui == null)
                return null;

            ui.transform.SetAsLastSibling();
            ui.userData = userData;
            ui.Show(useAnimation);

            // BaseUI baseUI = FindUI(path);
            // if (baseUI == null)
            //     baseUI = Instantiate(path);
            //
            // if (baseUI == null)
            //     return null;
            //
            // if (baseUI.IsPlayingAnimation)
            //     return baseUI;
            //
            // int code = GetHashCode(path);
            // if (!_instanceUIs.Contains(code))
            // {
            //     _instanceUIs.Add(code);
            //     baseUI.transform.SetAsLastSibling();
            //     baseUI.OnShow();
            //     SortUI();
            //     RefreshUI();
            // }
            //
            // if (baseUI.GetType().IsSubclassOf(typeof(Pop)))
            //     OnPopShowEvt?.Invoke();
            //
            // OnUIChangedEvt?.Invoke();
            //
            // return baseUI;
            return ui;
        }

        public PopUI GetPopUI()
        {
            foreach (var ui in _instanceUIs)
            {
                if (ui.IsState(Panel.StateEnum.Show | Panel.StateEnum.Shown) && ui is PopUI popUI)
                    return popUI;
            }

            return null;
        }

        public bool HasPopUI()
        {
            foreach (var ui in _instanceUIs)
            {
                if (ui.IsState(Panel.StateEnum.Show | Panel.StateEnum.Shown) && ui is PopUI)
                    return true;
            }

            return false;
        }

        // public void HideTopPopUI(bool useAnimation = false)
        // {
        //     for (int i = 0; i < _instanceUIs.Count; i++)
        //     {
        //         var ui = _instanceUIs[i];
        //         if (ui is PopUI pop)
        //         {
        //             if (pop.allowPopClose)
        //                 pop.Hide(useAnimation);
        //
        //             break;
        //         }
        //     }
        // }
        //
        // public void CloseTopPopUI(bool useAnimation = false)
        // {
        //     for (int i = 0; i < _instanceUIs.Count; i++)
        //     {
        //         var ui = _instanceUIs[i];
        //         if (ui is PopUI pop)
        //         {
        //             if (pop.allowPopClose)
        //                 pop.Close(useAnimation);
        //
        //             break;
        //         }
        //     }
        // }
        public T Create<T>() where T : Panel
        {
            var path = GetPath(typeof(T));
            var ui = FindUI(path);

            if (ui == null)
                ui = Instantiate(path);

            return ui as T;
        }

        public T Show<T>(bool useAnimation = true) where T : Panel
        {
            return Show(GetPath(typeof(T)), useAnimation) as T;
        }

        string GetPath(Type t)
        {
            var bind = t.GetCustomAttribute<BindUIPathAttribute>();
            var path = $"Assets/UI/{t.Name}.prefab";
            if (bind != null)
                path = bind.path;

            return path;
        }

        public Panel Show(Panel ui)
        {
            if (ui == null)
                return null;

            return Show(GetPath(ui.GetType()));
        }

        public Panel Show(Type ui)
        {
            if (ui == null)
                return null;

            return Show(GetPath(ui));
        }

        public void Destroy(Panel ui, bool force = false)
        {
            if (ui == null)
                return;

            if (_persistenceUI.Contains(ui) && !force)
                return;

            _instanceUIs.Remove(ui);
            _persistenceUI.Remove(ui);
            Object.Destroy(ui.gameObject);
            RefreshUI();
        }

        public void Destroy(string path)
        {
            Panel panel = FindUI(path);
            Destroy(panel);
        }

        public void Destroy<T>() where T : Panel
        {
            Destroy(GetPath(typeof(T)));
        }

        public void Hide<T>(bool useAnimation = true) where T : Panel
        {
            Hide(GetPath(typeof(T)), useAnimation);
        }

        public void Hide(Panel ui, bool useAnimation = true)
        {
            if (ui && ui.IsState(Panel.StateEnum.Shown))
                ui.Hide(useAnimation);
        }

        public void Hide(string path, bool useAnimation = true)
        {
            var ui = FindUI(path);
            if (ui && ui.IsState(Panel.StateEnum.Shown))
                ui.Hide(useAnimation);
        }

        Panel LoadUI(string path)
        {
            if (_loadedUI.TryGetValue(path, out var p))
                return p;

            // path = Path.Combine(DefaultFolder, path);
            var prefab = path.LoadAsset<GameObject>();
            if (prefab == null)
            {
                Log.Warning("加载UI失败:" + path);
                return null;
            }

            if (!prefab.TryGetComponent<Panel>(out p))
                return null;

            _loadedUI.Add(path, p);
            return p;
        }

        void LoadUI()
        {
            Log.Separator("加载UI");
            var tw = new Stopwatch();
            var ui = Resources.LoadAll<GameObject>(DefaultFolder);
            foreach (var variable in ui)
            {
                if (variable.TryGetComponent<Panel>(out var panel))
                {
                    _loadedUI.Add(variable.name, panel);
                    Log.Info("加载UI: " + variable.name);
                }
            }

            Log.Info($"耗时:{tw.Elapsed.TotalSeconds}s");
        }

        UniTask IInitializeAsync.OnInitializeAsync()
        {
            // for (int i = 0; i < LauncherSetting.instance.systemConfig.uiRoot.childCount; i++)
            //     Object.Destroy(LauncherSetting.instance.systemConfig.uiRoot.GetChild(i).gameObject);
            //
            // // InitBlurMask();
            // InitMask();
            // LoadUI();
            // // LoadPersistenceUI<ConsolePanel>();
            // // LoadPersistenceUI<Transition>();
            // // LoadPersistenceUI<BlackCurtainPanel>();
            //
            // XFramework.globalDispatcher.Register<InputEventDefines.OnUIKey>((x) =>
            // {
            //     if (x.uiKeyData.code == UIKeyCodeEnum.Exit && x.uiKeyData.state == KeyStateEnum.Down)
            //     {
            //         if (HasPopUI())
            //             AutoCloseTopPopUI(true);
            //     }
            // });
            return UniTask.CompletedTask;
        }

        public void Close<T>(bool useAnimation = true) where T : Panel
        {
            var ui = FindUI(GetPath(typeof(T)));
            if (ui)
                ui.Close(useAnimation);
        }

        public void Close(Type type, bool useAnimation = true)
        {
            var ui = FindUI(GetPath(type));
            if (ui)
                ui.Close(useAnimation);
        }

        public void Close(string path, bool useAnimation = true)
        {
            var ui = FindUI(path);
            if (ui)
                ui.Close(useAnimation);
        }

        public T ShowOrHide<T>() where T : Panel
        {
            var f = FindUI<T>();
            if (f == null)
                return Show<T>();

            if (f.IsState(Panel.StateEnum.Hidden))
            {
                Show(f);
                return f;
            }

            if (f.IsState(Panel.StateEnum.Shown))
            {
                Hide(f);
                return f;
            }

            return f;
        }

        public void AutoClosePopUI(PopUI pop, bool useAnimation)
        {
            if (pop.IsState(Panel.StateEnum.Show | Panel.StateEnum.Shown))
            {
                if (pop.autoCloseTopType == AutoCloseTopTypeEnum.Close)
                {
                    pop.Close(useAnimation);
                    return;
                }
                else if (pop.autoCloseTopType == AutoCloseTopTypeEnum.Hide)
                {
                    pop.Hide(useAnimation);
                    return;
                }
                else if (pop.autoCloseTopType == AutoCloseTopTypeEnum.None)
                {
                    return;
                }
            }
        }

        public void AutoCloseTopPopUI(bool useAnimation = true)
        {
            bool flag = false;
            for (int i = 0; i < _instanceUIs.Count; i++)
            {
                var ui = _instanceUIs[i];
                if (ui.IsState(Panel.StateEnum.Show | Panel.StateEnum.Shown) && ui is PopUI pop)
                {
                    if (pop.autoCloseTopType == AutoCloseTopTypeEnum.Close)
                    {
                        flag = true;
                        pop.Close(useAnimation);
                        break;
                    }
                    else if (pop.autoCloseTopType == AutoCloseTopTypeEnum.Hide)
                    {
                        flag = true;
                        pop.Hide(useAnimation);
                        break;
                    }
                    else if (pop.autoCloseTopType == AutoCloseTopTypeEnum.None)
                    {
                        break;
                    }
                }
            }
        }

        #region MainUI Management

        /// <summary>
        /// 显示MainUI（保留历史栈）
        /// </summary>
        public T ShowMainUI<T>(bool useAnimation = true, object userData = null) where T : MainUI
        {
            var newMain = Show<T>(useAnimation) as MainUI;
            if (newMain != null && newMain != _currentMainUI)
            {
                var oldMain = _currentMainUI;
                if (oldMain != null)
                {
                    _mainUIHistory.Push(oldMain);
                    oldMain.Hide(useAnimation);
                }

                NotifyMainUIChange(oldMain, newMain);
                _currentMainUI = newMain;
            }

            if (newMain != null)
                newMain.userData = userData;

            return newMain as T;
        }

        /// <summary>
        /// 切换MainUI（关闭旧的MainUI）
        /// </summary>
        public T SwitchMainUI<T>(bool useAnimation = true, object userData = null) where T : MainUI
        {
            var oldMain = _currentMainUI;
            var newMain = Show<T>(useAnimation) as MainUI;

            if (newMain != null && newMain != oldMain)
            {
                NotifyMainUIChange(oldMain, newMain);
                _currentMainUI = newMain;

                if (oldMain != null)
                    oldMain.Close(useAnimation);

                // 切换时清空历史
                _mainUIHistory.Clear();
            }

            if (newMain != null)
                newMain.userData = userData;

            return newMain as T;
        }

        /// <summary>
        /// 返回上一个MainUI
        /// </summary>
        public MainUI GoBackMainUI(bool useAnimation = true)
        {
            if (_mainUIHistory.Count == 0)
                return null;

            var oldMain = _currentMainUI;
            var newMain = _mainUIHistory.Pop();

            if (newMain != null)
            {
                NotifyMainUIChange(oldMain, newMain);
                _currentMainUI = newMain;
                newMain.Show(useAnimation);

                if (oldMain != null)
                    oldMain.Close(useAnimation);
            }

            return newMain;
        }

        /// <summary>
        /// 清空MainUI历史栈
        /// </summary>
        public void ClearMainUIHistory()
        {
            _mainUIHistory.Clear();
        }

        /// <summary>
        /// 获取MainUI历史栈深度
        /// </summary>
        public int MainUIHistoryCount => _mainUIHistory.Count;

        /// <summary>
        /// 通知MainUI切换，处理附加UI
        /// </summary>
        private void NotifyMainUIChange(MainUI oldMain, MainUI newMain)
        {
            if (oldMain == newMain)
                return;

            var uisToProcess = new List<Panel>(_instanceUIs);
            foreach (var ui in uisToProcess)
            {
                // 跳过MainUI本身
                if (ui is MainUI)
                    continue;

                // 跳过持久化UI
                if (_persistenceUI.Contains(ui))
                    continue;

                // 根据UI的mainUIChangeBehavior处理
                switch (ui.mainUIChangeBehavior)
                {
                    case MainUIChangeBehavior.None:
                        // 不处理
                        break;
                    case MainUIChangeBehavior.Hide:
                        if (ui.IsState(Panel.StateEnum.Show | Panel.StateEnum.Shown))
                            ui.Hide(true);
                        break;
                    case MainUIChangeBehavior.Close:
                        if (!ui.IsState(Panel.StateEnum.Close))
                            ui.Close(true);
                        break;
                }
            }
        }

        #endregion
    }
}