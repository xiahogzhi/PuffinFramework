using System.Collections.Generic;
using UnityEngine;

namespace Puffin.Modules.GameDevKit.Runtime.Pool
{
    /// <summary>
    /// GameObject 对象池
    /// <para>专门用于管理 Unity GameObject 的复用</para>
    /// </summary>
    /// <example>
    /// <code>
    /// // 创建池
    /// var bulletPool = new GameObjectPool(bulletPrefab, poolParent, maxSize: 100);
    ///
    /// // 预热（可选）
    /// bulletPool.Prewarm(20);
    ///
    /// // 生成对象
    /// var bullet = bulletPool.Spawn(spawnPos, Quaternion.identity);
    ///
    /// // 生成并获取组件
    /// var bulletComp = bulletPool.Spawn&lt;Bullet&gt;(spawnPos, Quaternion.identity);
    ///
    /// // 回收对象
    /// bulletPool.Despawn(bullet);
    ///
    /// // 清空池（销毁所有对象）
    /// bulletPool.Clear();
    /// </code>
    /// </example>
    public class GameObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly Stack<GameObject> _pool = new();
        private readonly int _maxSize;

        /// <summary>池中空闲对象数量</summary>
        public int CountInactive => _pool.Count;

        /// <summary>池创建的总对象数量</summary>
        public int CountAll { get; private set; }

        /// <summary>
        /// 创建 GameObject 对象池
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <param name="parent">池对象的父节点（可选）</param>
        /// <param name="maxSize">池的最大容量，超出后归还的对象将被销毁</param>
        public GameObjectPool(GameObject prefab, Transform parent = null, int maxSize = 1000)
        {
            _prefab = prefab;
            _parent = parent;
            _maxSize = maxSize;
        }

        /// <summary>
        /// 从池中获取 GameObject
        /// </summary>
        /// <param name="position">生成位置</param>
        /// <param name="rotation">生成旋转</param>
        /// <returns>激活的 GameObject</returns>
        public GameObject Spawn(Vector3 position = default, Quaternion rotation = default)
        {
            GameObject go;
            if (_pool.Count > 0)
            {
                go = _pool.Pop();
                go.transform.SetPositionAndRotation(position, rotation);
            }
            else
            {
                go = Object.Instantiate(_prefab, position, rotation, _parent);
                CountAll++;
            }

            go.SetActive(true);

            // 调用所有 IPoolable 组件的 OnSpawn
            foreach (var poolable in go.GetComponents<IPoolable>())
                poolable.OnSpawn();

            return go;
        }

        /// <summary>
        /// 从池中获取 GameObject 并返回指定组件
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="position">生成位置</param>
        /// <param name="rotation">生成旋转</param>
        /// <returns>指定类型的组件</returns>
        public T Spawn<T>(Vector3 position = default, Quaternion rotation = default) where T : Component
        {
            return Spawn(position, rotation).GetComponent<T>();
        }

        /// <summary>
        /// 将 GameObject 归还到池中
        /// <para>对象会被禁用并移动到池父节点下</para>
        /// </summary>
        /// <param name="go">要归还的 GameObject</param>
        public void Despawn(GameObject go)
        {
            if (go == null) return;

            // 调用所有 IPoolable 组件的 OnDespawn
            foreach (var poolable in go.GetComponents<IPoolable>())
                poolable.OnDespawn();

            go.SetActive(false);
            go.transform.SetParent(_parent);

            if (_pool.Count < _maxSize)
                _pool.Push(go);
            else
                Object.Destroy(go);
        }

        /// <summary>
        /// 预热池，预先创建指定数量的对象
        /// <para>适合在加载界面调用，避免运行时卡顿</para>
        /// </summary>
        /// <param name="count">预创建数量</param>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count && _pool.Count < _maxSize; i++)
            {
                var go = Object.Instantiate(_prefab, _parent);
                go.SetActive(false);
                CountAll++;
                _pool.Push(go);
            }
        }

        /// <summary>
        /// 清空池，销毁所有池中的对象
        /// </summary>
        public void Clear()
        {
            while (_pool.Count > 0)
                Object.Destroy(_pool.Pop());
            CountAll = 0;
        }
    }
}
