using System;
using System.Collections.Generic;

namespace Puffin.Runtime.Tools.FSM
{
    /// <summary>
    /// 通用状态机
    /// <para>管理状态的切换和更新</para>
    /// </summary>
    /// <typeparam name="TState">状态基类型，必须实现 IState</typeparam>
    /// <example>
    /// <code>
    /// // 1. 定义状态
    /// public class IdleState : IState
    /// {
    ///     public void OnEnter() => Debug.Log("进入待机");
    ///     public void OnUpdate(float dt) { }
    ///     public void OnExit() => Debug.Log("退出待机");
    /// }
    ///
    /// public class RunState : IState
    /// {
    ///     public void OnEnter() => Debug.Log("进入奔跑");
    ///     public void OnUpdate(float dt) { }
    ///     public void OnExit() => Debug.Log("退出奔跑");
    /// }
    ///
    /// // 2. 创建状态机并添加状态
    /// var fsm = new StateMachine&lt;IState&gt;();
    /// fsm.AddState(new IdleState());
    /// fsm.AddState(new RunState());
    ///
    /// // 3. 切换状态
    /// fsm.ChangeState&lt;IdleState&gt;();  // 进入待机状态
    /// fsm.ChangeState&lt;RunState&gt;();   // 切换到奔跑状态
    ///
    /// // 4. 在 Update 中调用
    /// void Update()
    /// {
    ///     fsm.Update(Time.deltaTime);
    /// }
    ///
    /// // 5. 查询状态
    /// if (fsm.IsInState&lt;IdleState&gt;()) { }
    /// var idle = fsm.GetState&lt;IdleState&gt;();
    ///
    /// // 6. 返回上一个状态
    /// fsm.RevertToPreviousState();
    /// </code>
    /// </example>
    public class StateMachine<TState> where TState : class, IState
    {
        private readonly Dictionary<Type, TState> _states = new();
        private TState _currentState;
        private TState _previousState;

        /// <summary>当前状态</summary>
        public TState CurrentState => _currentState;

        /// <summary>上一个状态</summary>
        public TState PreviousState => _previousState;

        /// <summary>当前状态的类型</summary>
        public Type CurrentStateType => _currentState?.GetType();

        /// <summary>
        /// 添加状态到状态机
        /// </summary>
        /// <param name="state">状态实例</param>
        public void AddState(TState state)
        {
            _states[state.GetType()] = state;
        }

        /// <summary>
        /// 切换到指定类型的状态
        /// </summary>
        /// <typeparam name="T">目标状态类型</typeparam>
        /// <exception cref="Exception">状态未找到时抛出</exception>
        public void ChangeState<T>() where T : TState
        {
            if (!_states.TryGetValue(typeof(T), out var newState))
                throw new Exception($"State {typeof(T).Name} not found");

            _previousState = _currentState;
            _currentState?.OnExit();
            _currentState = newState;
            _currentState.OnEnter();
        }

        /// <summary>
        /// 切换到指定类型的状态
        /// </summary>
        /// <param name="stateType">目标状态类型</param>
        /// <exception cref="Exception">状态未找到时抛出</exception>
        public void ChangeState(Type stateType)
        {
            if (!_states.TryGetValue(stateType, out var newState))
                throw new Exception($"State {stateType.Name} not found");

            _previousState = _currentState;
            _currentState?.OnExit();
            _currentState = newState;
            _currentState.OnEnter();
        }

        /// <summary>
        /// 更新当前状态，应在 Update 中调用
        /// </summary>
        /// <param name="deltaTime">帧间隔时间</param>
        public void Update(float deltaTime)
        {
            _currentState?.OnUpdate(deltaTime);
        }

        /// <summary>
        /// 检查当前是否处于指定状态
        /// </summary>
        /// <typeparam name="T">状态类型</typeparam>
        /// <returns>是否处于该状态</returns>
        public bool IsInState<T>() where T : TState => _currentState is T;

        /// <summary>
        /// 获取指定类型的状态实例
        /// </summary>
        /// <typeparam name="T">状态类型</typeparam>
        /// <returns>状态实例，未找到返回 null</returns>
        public T GetState<T>() where T : TState
        {
            return _states.TryGetValue(typeof(T), out var state) ? (T)state : default;
        }

        /// <summary>
        /// 返回上一个状态
        /// </summary>
        public void RevertToPreviousState()
        {
            if (_previousState != null)
                ChangeState(_previousState.GetType());
        }
    }

    /// <summary>
    /// 带拥有者的状态机
    /// <para>状态可以访问拥有者对象，适合角色控制等场景</para>
    /// </summary>
    /// <typeparam name="TOwner">拥有者类型</typeparam>
    /// <typeparam name="TState">状态基类型</typeparam>
    /// <example>
    /// <code>
    /// // 1. 定义带拥有者的状态
    /// public class PlayerIdleState : IState&lt;Player&gt;
    /// {
    ///     public void OnEnter(Player owner) => owner.Animator.Play("Idle");
    ///     public void OnUpdate(Player owner, float dt)
    ///     {
    ///         if (owner.Input.HasMovement)
    ///             owner.FSM.ChangeState&lt;PlayerMoveState&gt;();
    ///     }
    ///     public void OnExit(Player owner) { }
    /// }
    ///
    /// // 2. 在 Player 类中使用
    /// public class Player : MonoBehaviour
    /// {
    ///     public StateMachine&lt;Player, IState&lt;Player&gt;&gt; FSM { get; private set; }
    ///
    ///     void Start()
    ///     {
    ///         FSM = new StateMachine&lt;Player, IState&lt;Player&gt;&gt;(this);
    ///         FSM.AddState(new PlayerIdleState());
    ///         FSM.AddState(new PlayerMoveState());
    ///         FSM.ChangeState&lt;PlayerIdleState&gt;();
    ///     }
    ///
    ///     void Update()
    ///     {
    ///         FSM.Update(Time.deltaTime);
    ///     }
    /// }
    /// </code>
    /// </example>
    public class StateMachine<TOwner, TState> where TState : class, IState<TOwner>
    {
        private readonly TOwner _owner;
        private readonly Dictionary<Type, TState> _states = new();
        private TState _currentState;

        /// <summary>当前状态</summary>
        public TState CurrentState => _currentState;

        /// <summary>当前状态的类型</summary>
        public Type CurrentStateType => _currentState?.GetType();

        /// <summary>
        /// 创建带拥有者的状态机
        /// </summary>
        /// <param name="owner">状态机拥有者</param>
        public StateMachine(TOwner owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// 添加状态到状态机
        /// </summary>
        /// <param name="state">状态实例</param>
        public void AddState(TState state)
        {
            _states[state.GetType()] = state;
        }

        /// <summary>
        /// 切换到指定类型的状态
        /// </summary>
        /// <typeparam name="T">目标状态类型</typeparam>
        /// <exception cref="Exception">状态未找到时抛出</exception>
        public void ChangeState<T>() where T : TState
        {
            if (!_states.TryGetValue(typeof(T), out var newState))
                throw new Exception($"State {typeof(T).Name} not found");

            _currentState?.OnExit(_owner);
            _currentState = newState;
            _currentState.OnEnter(_owner);
        }

        /// <summary>
        /// 更新当前状态，应在 Update 中调用
        /// </summary>
        /// <param name="deltaTime">帧间隔时间</param>
        public void Update(float deltaTime)
        {
            _currentState?.OnUpdate(_owner, deltaTime);
        }

        /// <summary>
        /// 检查当前是否处于指定状态
        /// </summary>
        /// <typeparam name="T">状态类型</typeparam>
        /// <returns>是否处于该状态</returns>
        public bool IsInState<T>() where T : TState => _currentState is T;
    }
}
