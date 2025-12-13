namespace Puffin.Runtime.Tools.Pool
{
    /// <summary>
    /// 可池化对象接口
    /// <para>实现此接口的对象在从池中获取/归还时会收到回调</para>
    /// </summary>
    /// <example>
    /// <code>
    /// public class Bullet : MonoBehaviour, IPoolable
    /// {
    ///     public void OnSpawn()
    ///     {
    ///         // 从池中取出时调用，初始化状态
    ///         GetComponent&lt;Rigidbody&gt;().velocity = Vector3.zero;
    ///     }
    ///
    ///     public void OnDespawn()
    ///     {
    ///         // 归还到池中时调用，清理状态
    ///         StopAllCoroutines();
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IPoolable
    {
        /// <summary>
        /// 从池中取出时调用
        /// </summary>
        void OnSpawn();

        /// <summary>
        /// 归还到池中时调用
        /// </summary>
        void OnDespawn();
    }
}
