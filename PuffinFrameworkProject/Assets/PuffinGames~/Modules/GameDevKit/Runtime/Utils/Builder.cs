using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFrameworks.Utils
{
    public abstract class Builder<T, K> : IDisposable where T : Builder<T, K>, new() where K : class
    {
        public bool isDisposed { private set; get; }
        private static Queue<T> pool { set; get; } = new();

        public static T Get()
        {
            if (pool.Count > 0)
            {
                var p = pool.Dequeue();
                p.isDisposed = false;
                return p;
            }

            return new T();
        }
        


        protected void CheckIfDispose()
        {
            if (isDisposed)
                throw new Exception("构建器已销毁无法使用!");
        }

        public void Dispose()
        {
            CheckIfDispose();
            isDisposed = true;
            OnDispose();
            pool.Enqueue((T)this);
        }

        protected virtual void OnDispose()
        {
        }

        public K Build(bool autoDispose = true)
        {
            CheckIfDispose();
            K t = null;
            try
            {
                t = OnBuild();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (autoDispose)
                Dispose();

            return t;
        }

        protected virtual K OnBuild()
        {
            return null;
        }
    }
}