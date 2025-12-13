using UnityEngine;

namespace Puffin.Runtime.Core
{
    /// <summary>
    /// Runtime 的 MonoBehaviour 载体，负责转发 Unity 生命周期事件
    /// </summary>
    public class PuffinFrameworkRuntimeBehaviour : MonoBehaviour
    {
        private GameSystemRuntime _runtime;

        public void Initialize(GameSystemRuntime runtime)
        {
            _runtime = runtime;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            _runtime?.Update(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            _runtime?.FixedUpdate(Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            _runtime?.LateUpdate(Time.deltaTime);
        }

        private void OnApplicationFocus(bool focus)
        {
            _runtime?.OnApplicationFocus(focus);
        }

        private void OnApplicationPause(bool pause)
        {
            _runtime?.OnApplicationPause(pause);
        }

        private void OnApplicationQuit()
        {
            _runtime?.OnApplicationQuit();
        }
    }
}
