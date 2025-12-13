using System;
using System.Collections.Generic;

namespace Puffin.Runtime.Tools.Pool
{
    /// <summary>
    /// 通用对象池
    /// <para>用于管理普通 C# 对象的复用，减少 GC 压力</para>
    /// </summary>
    /// <typeparam name="T">池化对象类型</typeparam>
    /// <example>
    /// <code>
    /// // 基础用法
    /// var pool = new ObjectPool&lt;MyClass&gt;(() => new MyClass());
    /// var obj = pool.Spawn();      // 获取对象
    /// pool.Despawn(obj);           // 归还对象
    ///
    /// // 带回调的用法
    /// var pool = new ObjectPool&lt;StringBuilder&gt;(
    ///     createFunc: () => new StringBuilder(),
    ///     onSpawn: sb => sb.Clear(),           // 取出时清空
    ///     onDespawn: sb => sb.Clear(),         // 归还时清空
    ///     defaultCapacity: 10,
    ///     maxSize: 100
    /// );
    ///
    /// // 预热池
    /// pool.Prewarm(20);  // 预先创建 20 个对象
    /// </code>
    /// </example>
    public class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _pool = new();
        private readonly Func<T> _createFunc;
        private readonly Action<T> _onSpawn;
        private readonly Action<T> _onDespawn;
        private readonly int _maxSize;

        /// <summary>池中空闲对象数量</summary>
        public int CountInactive => _pool.Count;

        /// <summary>池创建的总对象数量</summary>
        public int CountAll { get; private set; }

        /// <summary>当前正在使用的对象数量</summary>
        public int CountActive => CountAll - CountInactive;

        /// <summary>
        /// 创建对象池
        /// </summary>
        /// <param name="createFunc">创建对象的工厂方法</param>
        /// <param name="onSpawn">对象被取出时的回调</param>
        /// <param name="onDespawn">对象被归还时的回调</param>
        /// <param name="defaultCapacity">初始容量（暂未使用）</param>
        /// <param name="maxSize">池的最大容量，超出后归还的对象将被丢弃</param>
        public ObjectPool(Func<T> createFunc, Action<T> onSpawn = null, Action<T> onDespawn = null, int defaultCapacity = 10, int maxSize = 1000)
        {
            _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            _onSpawn = onSpawn;
            _onDespawn = onDespawn;
            _maxSize = maxSize;
        }

        /// <summary>
        /// 从池中获取对象
        /// <para>如果池为空，则创建新对象</para>
        /// </summary>
        /// <returns>池化对象</returns>
        public T Spawn()
        {
            T item;
            if (_pool.Count > 0)
            {
                item = _pool.Pop();
            }
            else
            {
                item = _createFunc();
                CountAll++;
            }

            _onSpawn?.Invoke(item);
            (item as IPoolable)?.OnSpawn();
            return item;
        }

        /// <summary>
        /// 将对象归还到池中
        /// </summary>
        /// <param name="item">要归还的对象</param>
        public void Despawn(T item)
        {
            if (item == null) return;

            _onDespawn?.Invoke(item);
            (item as IPoolable)?.OnDespawn();

            if (_pool.Count < _maxSize)
                _pool.Push(item);
        }

        /// <summary>
        /// 预热池，预先创建指定数量的对象
        /// </summary>
        /// <param name="count">预创建数量</param>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count && _pool.Count < _maxSize; i++)
            {
                var item = _createFunc();
                CountAll++;
                _pool.Push(item);
            }
        }

        /// <summary>
        /// 清空池中所有对象
        /// </summary>
        public void Clear()
        {
            _pool.Clear();
            CountAll = 0;
        }
    }
}
