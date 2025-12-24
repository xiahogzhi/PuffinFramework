using System;
using UnityEngine;
using XFrameworks.Runtime.Core;
using Object = UnityEngine.Object;

namespace XFrameworks.Utils
{
    public enum ParentOptionEnum
    {
        World,
        Custom,
        UI
    }

    public enum PositionOptionEnum
    {
        None,
        Local,
        World,
    }

    /// <summary>
    /// 预设实例化配置
    /// </summary>
    public class PrefabBuilder : Builder<PrefabBuilder, GameObject>
    {
        /// <summary>
        /// 预设
        /// </summary>
        private GameObject prefab { set; get; }

        /// <summary>
        /// 默认激活状态
        /// </summary>
        private bool defaultActive { set; get; } = true;

        /// <summary>
        /// 父对象设置选项
        /// </summary>
        private ParentOptionEnum parentOption { set; get; }

        /// <summary>
        /// 位置设置选项
        /// </summary>
        private PositionOptionEnum positionOption { set; get; }

        /// <summary>
        /// 位置
        /// </summary>
        private Vector3 position { set; get; }

        private Vector3 scale { set; get; } = new Vector3(1, 1, 1);

        private Quaternion rotate { set; get; } = Quaternion.identity;

        /// <summary>
        /// 父对象
        /// </summary>
        private Transform parent { set; get; }


        protected override GameObject OnBuild()
        {
            if (prefab == null)
                return null;

            bool flag = prefab.activeSelf;
            prefab.SetActive(false);

            Transform p = null;
            switch (parentOption)
            {
                case ParentOptionEnum.World:
                    break;
                case ParentOptionEnum.Custom:
                    p = parent;
                    break;
                case ParentOptionEnum.UI:
                    // p = LauncherSetting.instance.systemConfig.uiRoot;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var go = Object.Instantiate(prefab, p);
            go.transform.localScale = scale;
            go.transform.rotation = rotate;

            prefab.SetActive(flag);

            switch (positionOption)
            {
                case PositionOptionEnum.None:
                    break;
                case PositionOptionEnum.Local:
                    go.transform.localPosition = position;
                    break;
                case PositionOptionEnum.World:
                    go.transform.position = position;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            go.SetActive(defaultActive);

            return go;
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            this.position = Vector3.zero;
            this.parent = null;
            this.prefab = null;
            this.defaultActive = true;
            this.positionOption = PositionOptionEnum.None;
            this.parentOption = ParentOptionEnum.Custom;
            scale = Vector3.one;
            rotate = Quaternion.identity;
        }

        public PrefabBuilder SetParent(Transform p)
        {
            CheckIfDispose();
            parent = p;
            parentOption = ParentOptionEnum.Custom;
            return this;
        }
        
        public PrefabBuilder SetPrefabAddress(string path)
        {
            CheckIfDispose();
            prefab = path.LoadAsset<GameObject>();
            return this;
        }
        
        public PrefabBuilder SetPrefab(string path)
        {
            CheckIfDispose();
            prefab = Resources.Load<GameObject>(path);
            return this;
        }

        public PrefabBuilder SetPrefab(GameObject go)
        {
            CheckIfDispose();
            prefab = go;
            return this;
        }

        public PrefabBuilder SetRotate(Quaternion p)
        {
            CheckIfDispose();
            rotate = p;
            return this;
        }

        public PrefabBuilder SetScale(Vector3 p)
        {
            CheckIfDispose();
            scale = p;
            return this;
        }

        public PrefabBuilder SetPosition(Vector3 p, PositionOptionEnum option = PositionOptionEnum.World)
        {
            CheckIfDispose();
            position = p;
            positionOption = option;
            return this;
        }

        public PrefabBuilder SetParentUI()
        {
            CheckIfDispose();
            parentOption = ParentOptionEnum.UI;
            parent = null;
            return this;
        }

        public PrefabBuilder SetDefaultActive(bool flag)
        {
            CheckIfDispose();
            defaultActive = flag;
            return this;
        }
    }
}