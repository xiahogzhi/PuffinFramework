using System;
using System.Collections.Generic;
using UnityEngine;

namespace Puffin.Modules.Timer.Runtime
{
    /// <summary>
    /// 定时器
    /// <para>提供延迟执行、重复执行、进度回调等功能</para>
    /// <para>由 TimerSystem 自动驱动更新，无需手动调用 Update</para>
    /// </summary>
    /// <example>
    /// <code>
    /// // 延迟执行
    /// Timer.Delay(2f, () => Debug.Log("2秒后执行"));
    ///
    /// // 使用真实时间（不受 Time.timeScale 影响）
    /// Timer.Delay(1f, () => Debug.Log("暂停菜单中也会执行"), useRealTime: true);
    ///
    /// // 重复执行（无限次）
    /// Timer.Repeat(0.5f, () => Debug.Log("每0.5秒执行一次"));
    ///
    /// // 重复执行（指定次数）
    /// Timer.Repeat(1f, () => Debug.Log("执行"), repeatCount: 5);  // 执行5次后停止
    ///
    /// // 带进度回调的定时器
    /// Timer.Create(3f,
    ///     onUpdate: progress => slider.value = progress,  // 0 -> 1
    ///     onComplete: () => Debug.Log("完成")
    /// );
    ///
    /// // 控制定时器
    /// var timer = Timer.Delay(5f, () => { });
    /// timer.Pause();   // 暂停
    /// timer.Resume();  // 恢复
    /// timer.Cancel();  // 取消
    ///
    /// // 取消所有定时器
    /// Timer.CancelAll();
    /// </code>
    /// </example>
    public class Timer
    {
        private static readonly List<Timer> _timers = new();
        private static readonly List<Timer> _toAdd = new();
        private static readonly List<Timer> _toRemove = new();
        private static readonly Dictionary<GameObject, List<Timer>> _goTimers = new();
        private static bool _isUpdating;

        private float _elapsed;
        private readonly float _duration;
        private readonly float _interval;
        private readonly Action _onComplete;
        private readonly Action<float> _onUpdate;
        private readonly bool _useRealTime;
        private readonly bool _isRepeat;
        private int _repeatCount;
        private readonly int _maxRepeat;

        /// <summary>定时器是否正在运行</summary>
        public bool IsRunning { get; private set; }

        /// <summary>定时器是否暂停</summary>
        public bool IsPaused { get; private set; }

        /// <summary>当前进度 (0-1)</summary>
        public float Progress => Mathf.Clamp01(_elapsed / _duration);

        private Timer(float duration, Action onComplete, Action<float> onUpdate, bool useRealTime, bool isRepeat, int maxRepeat, float interval)
        {
            _duration = duration;
            _onComplete = onComplete;
            _onUpdate = onUpdate;
            _useRealTime = useRealTime;
            _isRepeat = isRepeat;
            _maxRepeat = maxRepeat;
            _interval = interval > 0 ? interval : duration;
            IsRunning = true;
        }

        /// <summary>
        /// 延迟执行
        /// </summary>
        /// <param name="duration">延迟时间（秒）</param>
        /// <param name="onComplete">完成时回调</param>
        /// <param name="useRealTime">是否使用真实时间（不受 TimeScale 影响）</param>
        /// <returns>定时器实例，可用于暂停/取消</returns>
        public static Timer Delay(float duration, Action onComplete, bool useRealTime = false)
        {
            var timer = new Timer(duration, onComplete, null, useRealTime, false, 0, 0);
            AddTimer(timer);
            return timer;
        }

        /// <summary>
        /// 重复执行
        /// </summary>
        /// <param name="interval">执行间隔（秒）</param>
        /// <param name="onComplete">每次执行的回调</param>
        /// <param name="repeatCount">重复次数，-1 表示无限重复</param>
        /// <param name="useRealTime">是否使用真实时间</param>
        /// <returns>定时器实例</returns>
        public static Timer Repeat(float interval, Action onComplete, int repeatCount = -1, bool useRealTime = false)
        {
            var timer = new Timer(interval, onComplete, null, useRealTime, true, repeatCount, interval);
            AddTimer(timer);
            return timer;
        }

        /// <summary>
        /// 创建带进度回调的定时器
        /// </summary>
        /// <param name="duration">持续时间（秒）</param>
        /// <param name="onUpdate">每帧回调，参数为进度 (0-1)</param>
        /// <param name="onComplete">完成时回调</param>
        /// <param name="useRealTime">是否使用真实时间</param>
        /// <returns>定时器实例</returns>
        public static Timer Create(float duration, Action<float> onUpdate, Action onComplete = null, bool useRealTime = false)
        {
            var timer = new Timer(duration, onComplete, onUpdate, useRealTime, false, 0, 0);
            AddTimer(timer);
            return timer;
        }

        /// <summary>
        /// 绑定到 GameObject，当 GameObject 销毁时自动取消定时器
        /// </summary>
        /// <param name="go">要绑定的 GameObject</param>
        /// <returns>定时器实例（链式调用）</returns>
        public Timer AddTo(GameObject go)
        {
            if (go == null) return this;

            if (!_goTimers.TryGetValue(go, out var list))
            {
                list = new List<Timer>();
                _goTimers[go] = list;
                go.GetOrAddComponent<TimerDestroyer>();
            }
            list.Add(this);
            return this;
        }

        /// <summary>
        /// 绑定到 Component 所属的 GameObject
        /// </summary>
        public Timer AddTo(Component component) => AddTo(component?.gameObject);

        /// <summary>
        /// 暂停定时器
        /// </summary>
        public void Pause() => IsPaused = true;

        /// <summary>
        /// 恢复定时器
        /// </summary>
        public void Resume() => IsPaused = false;

        /// <summary>
        /// 取消定时器
        /// </summary>
        public void Cancel()
        {
            IsRunning = false;
            if (_isUpdating)
                _toRemove.Add(this);
            else
                _timers.Remove(this);
        }

        private static void AddTimer(Timer timer)
        {
            if (_isUpdating)
                _toAdd.Add(timer);
            else
                _timers.Add(timer);
        }

        /// <summary>
        /// 更新所有定时器（由 TimerSystem 调用）
        /// </summary>
        internal static void UpdateAll()
        {
            _isUpdating = true;

            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                var timer = _timers[i];
                if (!timer.IsRunning)
                {
                    _timers.RemoveAt(i);
                    continue;
                }

                if (timer.IsPaused) continue;

                // 根据设置选择时间源
                var dt = timer._useRealTime ? Time.unscaledDeltaTime : Time.deltaTime;
                timer._elapsed += dt;

                // 调用进度回调
                timer._onUpdate?.Invoke(timer.Progress);

                // 检查是否完成
                if (timer._elapsed >= timer._interval)
                {
                    timer._onComplete?.Invoke();

                    if (timer._isRepeat)
                    {
                        // 重复模式：重置计时
                        timer._elapsed = 0;
                        timer._repeatCount++;

                        // 检查是否达到最大重复次数
                        if (timer._maxRepeat > 0 && timer._repeatCount >= timer._maxRepeat)
                            timer.IsRunning = false;
                    }
                    else
                    {
                        // 单次模式：标记完成
                        timer.IsRunning = false;
                    }
                }
            }

            _isUpdating = false;

            // 处理更新期间添加/移除的定时器
            foreach (var t in _toAdd) _timers.Add(t);
            foreach (var t in _toRemove) _timers.Remove(t);
            _toAdd.Clear();
            _toRemove.Clear();
        }

        /// <summary>
        /// 取消所有定时器
        /// </summary>
        public static void CancelAll()
        {
            foreach (var timer in _timers)
                timer.IsRunning = false;
            _timers.Clear();
            _toAdd.Clear();
            _toRemove.Clear();
            _goTimers.Clear();
        }

        /// <summary>
        /// 取消指定 GameObject 上的所有定时器
        /// </summary>
        public static void CancelAll(GameObject go)
        {
            if (go == null || !_goTimers.TryGetValue(go, out var list)) return;

            foreach (var timer in list)
                timer.Cancel();
            _goTimers.Remove(go);
        }
    }

    internal class TimerDestroyer : MonoBehaviour
    {
        private void OnDestroy() => Timer.CancelAll(gameObject);
    }

    internal static class GameObjectExtensions
    {
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            return go.TryGetComponent<T>(out var comp) ? comp : go.AddComponent<T>();
        }
    }
}
