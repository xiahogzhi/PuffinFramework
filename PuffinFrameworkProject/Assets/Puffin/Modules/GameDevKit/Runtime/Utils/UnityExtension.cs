using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Puffin.Runtime.Core;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR

#endif

namespace XFrameworks.Runtime.Core
{
    public static class UnityExtension
    {

        public static GameObject LoadPrefab(string path)
        {
            return LoadAsset<GameObject>(path);
        }

        public static T LoadAsset<T>(this string path) where T : Object
        {
            return PuffinFramework.ResourcesLoader.Load<T>(path);
        }

        public static T TryAddComponent<T>(this GameObject go) where T : Component
        {
            if (go.TryGetComponent<T>(out var p))
                return p;

            return go.gameObject.AddComponent<T>();
        }

        public static T TryAddComponent<T>(this MonoBehaviour go) where T : Component
        {
            if (go.TryGetComponent<T>(out var p))
                return p;

            return go.gameObject.AddComponent<T>();
        }


        public static Component TryAddComponent(this GameObject go, Type type)
        {
            if (go == null)
                return null;
            if (go.TryGetComponent(type, out var p))
                return p;

            return go.AddComponent(type);
        }


        public static IEnumerator PlayCoroutine(this Animator animator, string animation)
        {
            animator.Play(animation);
            yield return WaitAnimation(animator, animation);
        }

        public static IEnumerator WaitAnimation(this Animator animator, string animation)
        {
            yield return null;
            while (true)
            {
                var c = animator.GetCurrentAnimatorStateInfo(0);
                if (!c.IsName(animation) || c.normalizedTime % 1 >= 0.99f)
                    yield break;

                yield return null;
            }
        }


        // public static void ConvertBoxToWorld(GameObject go)
        // {
        //     var bs = go.GetComponent<BoxCollider>();
        //
        //     var s = bs.size;
        //     (s.z, s.y) = (s.y, s.z);
        //     bs.size = s;
        //
        //     var c = bs.center;
        //     (c.z, c.y) = (c.y, c.z);
        //     bs.center = c;
        //
        //     if (!go.GetComponent<ModelTransform>())
        //     {
        //         var p = go.transform.position;
        //         (p.z, p.y) = (p.y, p.z);
        //         go.transform.position = p;
        //     }
        // }

        // /// <summary>
        // /// 处理通用伤害流
        // /// </summary>
        // /// <param name="cm"></param>
        // /// <param name="infos"></param>
        // public static void ProcessCommonDamageFlow(IDamageFlow cm, List<SelectInfo> infos)
        // {
        //     // if (infos == null || infos.Count <= 0)
        //     //     return;
        //     //
        //     // var flow = cm.GetDamageFlow();
        //     // var entity = cm.GetEntity();
        //     // bool hasDamage = false;
        //     // foreach (var ts in infos)
        //     // {
        //     //     var target = ts.Entity;
        //     //     if (target.IsDead())
        //     //         continue;
        //     //
        //     //     //创建打击特效
        //     //     CreateEffect(flow.HitEffect, ts.HitPoint);
        //     //
        //     //     //播放打击音效
        //     //     SEMgr.Instance.PlaySE(flow.HitSound,
        //     //         target.GetHurtSoundMaterial(),
        //     //         ts.HitPoint);
        //     //
        //     //     DamagePackage dp =
        //     //         DamagePackage.Create(cm, ts.HitPoint);
        //     //
        //     //     DamageResult d = entity.DamageTarget(target, dp);
        //     //
        //     //     if (flow.FrameFreezeTarget)
        //     //         target.FreezeFrame(flow.FrameFreezeTime * cm.GetScale());
        //     //
        //     //     if (d.Code != DamageResultCodeEnum.Success)
        //     //         continue;
        //     //
        //     //     if (dp.IsBreakSkill)
        //     //         ts.Entity.BreakSkill();
        //     //
        //     //     target.AddStiff(flow.StiffTime);
        //     //
        //     //     flow.CompositeForce.AddForce(cm.GetSelector(), entity, target, cm.GetFace());
        //     //
        //     //     cm.OnProcessTarget(ts);
        //     //
        //     //     foreach (var VARIABLE in flow.Actions)
        //     //     {
        //     //         VARIABLE.DoAction(cm, target);
        //     //     }
        //     //
        //     //     if (!target.HasTag(EntityTagEnum.Decorate))
        //     //         hasDamage = true;
        //     // }
        //     //
        //     // if (flow.FrameFreezeSelf)
        //     // {
        //     //     cm.FrameFreeze(flow.FrameFreezeTime);
        //     // }
        //     //
        //     // if (flow.ShakeDuration > 0)
        //     //     GameCamera.Shake(flow.ShakeDuration, flow.ShakeLength);
        //     //
        //     //
        //     // cm.OnFinish(hasDamage);
        // }



        public static Vector3 RotateX(Vector3 dir, float angle)
        {
            Vector3 r = new Vector3();

            angle *= Mathf.Deg2Rad;

            r.y = dir.y * Mathf.Cos(angle) - dir.z * Mathf.Sin(angle);
            r.z = dir.y * Mathf.Sin(angle) + dir.z * Mathf.Cos(angle);

            return r;
        }

        public static Vector3 RotateY(Vector3 dir, float angle)
        {
            Vector3 r = new Vector3();

            angle *= Mathf.Deg2Rad;

            r.x = dir.x * Mathf.Cos(angle) + dir.z * Mathf.Sin(angle);
            r.z = -dir.x * Mathf.Sin(angle) + dir.z * Mathf.Cos(angle);

            return r;
        }

        public static Vector3 RotateZ(Vector3 dir, float angle)
        {
            Vector3 r = new Vector3();

            angle *= Mathf.Deg2Rad;

            r.x = dir.x * Mathf.Cos(angle) - dir.y * Mathf.Sin(angle);
            r.y = dir.x * Mathf.Sin(angle) + dir.y * Mathf.Cos(angle);

            return r;
        }

        public static string GetTransformPath(this Transform p)
        {
            string b = p.gameObject.name;
            while (p.parent != null)
            {
                p = p.parent;

                if (p != null)
                {
                    b = p.gameObject.name + "/" + b;
                }
            }

            return b;
        }

        public static List<FileInfo> GetFiles(string path)
        {
            DirectoryInfo di = new DirectoryInfo(path);

            if (di.Exists)
                return GetFiles(di);

            return null;
        }

        public static string FilePathToUnity(string windowsPath)
        {
            var path = windowsPath;
            path = path.Replace("\\", "/");
            path = path.Replace(Application.dataPath, "Assets");
            return path;
        }


        public static List<FileInfo> GetFiles(DirectoryInfo di, List<FileInfo> fi = null)
        {
            if (fi == null)
                fi = new List<FileInfo>();

            foreach (var variable in di.GetDirectories())
                GetFiles(variable, fi);

            foreach (var variable in di.GetFiles())
            {
                if (!variable.Name.EndsWith(".meta"))
                    fi.Add(variable);
            }

            return fi;
        }


        // public static EntityComponent CreateEntity(EntityData data, Action<EntityComponent> onInitialize = null)
        // {
        //     // string guid = data.ConfigGuid;
        //     // var f = VirtualConfigManager.Instance.FindConfig<EntityConfig>(guid);
        //     // if (f == null)
        //     // {
        //     //     Log.Warning("无法创建实体:" + guid);
        //     //     return null;
        //     // }
        //     //
        //     // var go = VirtualConfigManager.Instance.Instantiate<GameObject>(f);
        //     // if (go == null)
        //     // {
        //     //     Log.Warning("无法创建实体:" + guid);
        //     //     return null;
        //     // }
        //     //
        //     // EntityComponent ec = go.GetComponent<EntityComponent>();
        //     // if (ec == null)
        //     //     return null;
        //     //
        //     // ec.EntityData = data;
        //     //
        //     // //判断是否首次创建
        //     // bool isCreated = data.Data != null;
        //     //
        //     // onInitialize?.Invoke(ec);
        //     //
        //     // ec.OnInitialize();
        //     //
        //     // if (!isCreated)
        //     // {
        //     //     ec.transform.position = data.BornPosition;
        //     //     ec.OnCreate();
        //     // }
        //     // else
        //     // {
        //     //     ec.Load();
        //     // }
        //     //
        //     //
        //     // ec.OnLoaded();
        //     //
        //     // // if (GameManager.Instance.IsLoaded)
        //     // //     ec.OnRoomLoaded();
        //     //
        //     // return ec;
        //     throw new NotImplementedException();
        // }


        /// <summary>
        /// Vector2包含值
        /// </summary>
        /// <param name="range"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool Contains(this Vector2 range, float value)
        {
            return value >= range.x && value <= range.y;
        }

        public static T GetComponentOrChild<T>(this MonoBehaviour mo)
        {
            if (mo.TryGetComponent<T>(out var p))
                return p;

            return mo.GetComponentInChildren<T>(true);
        }


        public static T GetComponentOrChild<T>(this GameObject mo)
        {
            if (mo.TryGetComponent<T>(out var p))
                return p;

            return mo.GetComponentInChildren<T>();
        }


        // public static OperationStateType GetStateType(SkillBlockIdEnum type)
        // {
        //     switch (type)
        //     {
        //         case SkillBlockIdEnum.Skill1:
        //             return OperationStateType.UseSkill1;
        //             break;
        //         case SkillBlockIdEnum.Skill2:
        //             return OperationStateType.UseSkill2;
        //             break;
        //         case SkillBlockIdEnum.Skill3:
        //             return OperationStateType.UseSkill3;
        //             break;
        //         case SkillBlockIdEnum.Skill4:
        //             return OperationStateType.UseSkill4;
        //             break;
        //         case SkillBlockIdEnum.Attack:
        //             return OperationStateType.UseAttack;
        //             break;
        //         case SkillBlockIdEnum.Special:
        //             return OperationStateType.UseSpecialAttack;
        //             break;
        //         case SkillBlockIdEnum.Passive:
        //             break;
        //         default:
        //             throw new ArgumentOutOfRangeException(nameof(type), type, null);
        //     }
        //
        //     return OperationStateType.None;
        // }

        public static IEnumerator CheckAnimationEnd(Animator animator, float progress)
        {
            yield return null;

            float t = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            while (t <= progress)
            {
                t = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                yield return null;
            }
        }

        public static IEnumerator CheckAnimationEnd(Animator animator)
        {
            yield return null;

            float t = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            while (t <= 0.99f)
            {
                t = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                yield return null;
            }
        }

        public static IEnumerator CheckAnimationEnd(Animator animator, Action callback)
        {
            yield return null;

            float t = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            while (t <= 0.99f)
            {
                t = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                yield return null;
            }

            callback?.Invoke();
        }


        /// <summary>
        /// 调用方法
        /// </summary>
        /// <param name="target"></param>
        /// <param name="n"></param>
        /// <param name="param"></param>
        public static void Invoke(object target, string n, object[] param = null)
        {
            MethodInfo mi = target.GetType().GetMethod(n,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (mi != null)
                mi.Invoke(target, param);
        }


        /// <summary>
        /// 获取当前类型以及所有父类型的字段
        /// </summary>
        /// <returns></returns>
        public static FieldInfo[] GetAllFieldInfo(Type type,
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        {
            List<FieldInfo> allFieldInfo = new List<FieldInfo>();
            while (type != null)
            {
                FieldInfo[] fis = type.GetFields(flags);
                allFieldInfo.AddRange(fis);
                type = type.BaseType;
            }

            return allFieldInfo.ToArray();
        }

        /// <summary>
        /// unity的程序集
        /// </summary>
        private static Assembly _unityAssembly;


        /// <summary>
        /// 将int32转换为RGBA格式颜色
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public static Color HexToRgba(this int hex)
        {
            float r = ((hex >> 24) & 0xFF) / 255f;
            float g = ((hex >> 16) & 0xFF) / 255f;
            float b = ((hex >> 8) & 0xFF) / 255f;
            float a = ((hex) & 0xFF) / 255f;

            return new Color(r, g, b, a);
        }

        /// <summary>
        /// 将RGBA格式颜色转换成int32
        /// </summary>
        /// <param name="col"></param>
        /// <returns></returns>
        public static int RgbaToHex(this Color col)
        {
            int r = (int) (col.r * 255f) << 24;
            int g = (int) (col.g * 255f) << 16;
            int b = (int) (col.b * 255f) << 8;
            int a = (int) (col.a * 255f);

            return r | g | b | a;
        }

        public static string ToHtmlStringRGB(this Color col)
        {
            return ColorUtility.ToHtmlStringRGB(col);
        }

        public static string ToHtmlStringRGBA(this Color col)
        {
            return ColorUtility.ToHtmlStringRGBA(col);
        }

        /// <summary>
        /// 将RGB转换成int32
        /// </summary>
        /// <param name="col"></param>
        /// <returns></returns>
        public static int RGBToHex(this Color col)
        {
            int r = (int) (col.r * 255f) << 16;
            int g = (int) (col.g * 255f) << 8;
            int b = (int) (col.b * 255f);

            return r | g | b;
        }

        public static string ToCeilText(this float b)
        {
            return Mathf.CeilToInt(b).ToString();
        }


        /// <summary>
        /// 将int32转换成RGB颜色
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public static Color HexToRGB(this int hex)
        {
            float r = ((hex >> 16) & 0xFF) / 255f;
            float g = ((hex >> 8) & 0xFF) / 255f;
            float b = ((hex) & 0xFF) / 255f;

            return new Color(r, g, b, 1);
        }

        ///获取动画状态机animator的动画clip的播放持续时长
        public static float GetClipLength(this Animator animator, string clipName)
        {
            if (null == animator ||
                string.IsNullOrEmpty(clipName) ||
                null == animator.runtimeAnimatorController)
                return 0;

            var ac = animator.runtimeAnimatorController;
            // 获取所有的clips	
            var clips = ac.animationClips;
            if (null == clips || clips.Length <= 0) return 0;
            AnimationClip clip;
            for (int i = 0, len = clips.Length; i < len; ++i)
            {
                clip = ac.animationClips[i];
                if (null != clip && clip.name == clipName)
                    return clip.length;
            }

            return 0f;
        }

        public static async UniTask PlayAsync(this Animator animator, string clipName)
        {
            animator.Play(clipName,0,0);
            await animator.WaitAnimationAsync(clipName);
        }

        public static async UniTask WaitAnimationAsync(this Animator animator, string clipName)
        {
            if (animator == null)
                return;
            await UniTask.Yield();
            while (true)
            {
                await UniTask.Yield();

                var c = animator.GetCurrentAnimatorStateInfo(0);
                if (!c.IsName(clipName) || c.normalizedTime % 1 >= 0.99f)
                    return;
            }
        }

        public static async UniTask PlayAsync(this Animator animator, string clipName,
            CancellationToken cancellationToken)
        {
            animator.Play(clipName);
            await animator.WaitAnimationAsync(clipName, cancellationToken);
        }

        public static async UniTask WaitAnimationAsync(this Animator animator, string clipName,
            CancellationToken cancellationToken)
        {
            if (animator == null)
                return;

            while (true)
            {
                if (await UniTask.Yield(cancellationToken).SuppressCancellationThrow())
                    return;

                var c = animator.GetCurrentAnimatorStateInfo(0);
                if (!c.IsName(clipName) || c.normalizedTime % 1 >= 0.99f)
                    return;
            }
        }

    }
}