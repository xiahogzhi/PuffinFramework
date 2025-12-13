using Puffin.Runtime.Core;
using UnityEngine;

namespace Puffin.Runtime.Behaviours
{
    /// <summary>
    /// 游戏脚本基类，提供生命周期管理和事件收集功能
    /// <para>编辑器下自动进行引用赋值（字段名匹配子节点名）</para>
    /// </summary>
    public class GameScript : MonoBehaviour
    {

        /// <summary>是否处于运行时状态</summary>
        public bool isRuntime { get; private set; }

#if UNITY_EDITOR
        private bool _canDestroy;
#endif

        /// <summary>
        /// 自定义引用赋值逻辑，子类可重写
        /// </summary>
        /// <returns>是否有修改</returns>
        protected virtual bool CustomReference() => false;

        protected virtual void Awake()
        {
#if UNITY_EDITOR
            if (!PuffinFramework.IsApplicationStarted) return;
            _canDestroy = true;
#endif
            isRuntime = true;
            OnScriptInitialize();
        }

        protected virtual void Start()
        {
#if UNITY_EDITOR
            if (!PuffinFramework.IsApplicationStarted) return;
#endif

            
            OnScriptStart();
        }

        protected virtual void OnEnable()
        {
#if UNITY_EDITOR
            if (!PuffinFramework.IsApplicationStarted) return;
#endif
            OnScriptActivate();
        }

        protected virtual void OnDisable()
        {
#if UNITY_EDITOR
            if (!PuffinFramework.IsApplicationStarted) return;
#endif
            OnScriptDeactivate();
        }

        protected virtual void OnDestroy()
        {
#if UNITY_EDITOR
            if (!PuffinFramework.IsApplicationStarted || !_canDestroy) return;
#endif
            OnScriptEnd();
            OnEventUnRegister();
        }

        /// <summary>脚本初始化，在 Awake 时调用</summary>
        protected virtual void OnScriptInitialize() => OnEventRegister();

        /// <summary>脚本开始，在 Start 时调用</summary>
        protected virtual void OnScriptStart() { }

        /// <summary>脚本激活，在 OnEnable 时调用</summary>
        protected virtual void OnScriptActivate() { }

        /// <summary>脚本禁用，在 OnDisable 时调用</summary>
        protected virtual void OnScriptDeactivate() { }

        /// <summary>注册事件监听</summary>
        protected virtual void OnEventRegister() { }

        /// <summary>注销事件监听</summary>
        protected virtual void OnEventUnRegister() {}

        /// <summary>脚本结束，在 OnDestroy 时调用</summary>
        protected virtual void OnScriptEnd() {}

    }
}
