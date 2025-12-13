namespace Puffin.Runtime.Tools.FSM
{
    /// <summary>
    /// 状态接口
    /// <para>定义状态的基本生命周期方法</para>
    /// </summary>
    /// <example>
    /// <code>
    /// public class IdleState : IState
    /// {
    ///     public void OnEnter()
    ///     {
    ///         Debug.Log("进入待机状态");
    ///     }
    ///
    ///     public void OnUpdate(float deltaTime)
    ///     {
    ///         // 每帧更新逻辑
    ///     }
    ///
    ///     public void OnExit()
    ///     {
    ///         Debug.Log("退出待机状态");
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IState
    {
        /// <summary>
        /// 进入状态时调用
        /// </summary>
        void OnEnter();

        /// <summary>
        /// 状态更新，每帧调用
        /// </summary>
        /// <param name="deltaTime">帧间隔时间</param>
        void OnUpdate(float deltaTime);

        /// <summary>
        /// 退出状态时调用
        /// </summary>
        void OnExit();
    }

    /// <summary>
    /// 带拥有者的状态接口
    /// <para>状态可以访问拥有者对象，适合需要操作特定对象的场景</para>
    /// </summary>
    /// <typeparam name="T">拥有者类型</typeparam>
    /// <example>
    /// <code>
    /// public class PlayerIdleState : IState&lt;Player&gt;
    /// {
    ///     public void OnEnter(Player owner)
    ///     {
    ///         owner.Animator.Play("Idle");
    ///     }
    ///
    ///     public void OnUpdate(Player owner, float deltaTime)
    ///     {
    ///         if (owner.Input.HasMovement)
    ///             owner.FSM.ChangeState&lt;PlayerMoveState&gt;();
    ///     }
    ///
    ///     public void OnExit(Player owner)
    ///     {
    ///         // 清理逻辑
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IState<T>
    {
        /// <summary>
        /// 进入状态时调用
        /// </summary>
        /// <param name="owner">状态拥有者</param>
        void OnEnter(T owner);

        /// <summary>
        /// 状态更新，每帧调用
        /// </summary>
        /// <param name="owner">状态拥有者</param>
        /// <param name="deltaTime">帧间隔时间</param>
        void OnUpdate(T owner, float deltaTime);

        /// <summary>
        /// 退出状态时调用
        /// </summary>
        /// <param name="owner">状态拥有者</param>
        void OnExit(T owner);
    }
}
