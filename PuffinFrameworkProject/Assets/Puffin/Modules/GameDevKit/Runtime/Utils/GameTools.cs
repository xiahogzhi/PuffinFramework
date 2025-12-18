using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Puffin.Runtime.Tools;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace XFrameworks.Utils
{
    public static partial class GameTools
    {
        private static string _currentScene;

        public static void CloneOverwrite(this object obj, object target)
        {
            var json = JsonUtility.ToJson(obj);
            JsonUtility.FromJsonOverwrite(json, target);
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawRect(this Rect rect, Color color, float duration = 0)
        {
            Vector3 topLeft = new Vector3(rect.xMin, rect.yMin, 0);
            Vector3 topRight = new Vector3(rect.xMax, rect.yMin, 0);
            Vector3 bottomLeft = new Vector3(rect.xMin, rect.yMax, 0);
            Vector3 bottomRight = new Vector3(rect.xMax, rect.yMax, 0);

            // 使用 Debug.DrawLine 绘制矩形的四条边
            if (duration > 0)
            {
                Debug.DrawLine(topLeft, topRight, color, duration); // 上边
                Debug.DrawLine(topRight, bottomRight, color, duration); // 右边
                Debug.DrawLine(bottomRight, bottomLeft, color, duration); // 下边
                Debug.DrawLine(bottomLeft, topLeft, color, duration); // 左边
            }
            else
            {
                Debug.DrawLine(topLeft, topRight, color); // 上边
                Debug.DrawLine(topRight, bottomRight, color); // 右边
                Debug.DrawLine(bottomRight, bottomLeft, color); // 下边
                Debug.DrawLine(bottomLeft, topLeft, color); // 左边
            }
        }

        public static Color SetAlpha(this Color color, float alpha)
        {
            var c = color;
            c.a = alpha;
            return c;
        }

 

        public static void SetValue(this Dictionary<string, object> data, string key, object value)
        {
            data[key] = value;
        }


        public static T GetValue<T>(this Dictionary<string, object> data, string key, T defaultValue = default)
        {
            try
            {
                if (data.TryGetValue(key, out var value))
                {
                    return (T)value;
                }

                return defaultValue;
            }
            catch (Exception e)
            {
                Log.Warning("Failed to load data: " + key);
                return defaultValue;
            }
        }

 

        public static async UniTask PostAction(float delay, Action action)
        {
            await UniTask.Delay(delay.ToTimeSpan());
            action();
        }

        private static Dictionary<string, object> parseCache { set; get; } = new();

        public static object ParseByType(Type type, string param, string info = null)
        {
            if (string.IsNullOrEmpty(param))
                return null;

            var id = type.GetHashCode();
            var id2 = param.GetHashCode();
            var rid = id + "_" + id2;
            if (parseCache.TryGetValue(rid, out var p))
                return p;

            try
            {
                if (type.IsArray)
                {
                    var el = type.GetElementType();
                    if (el == null)
                        return null;

                    var r = param.Split(",");

                    var m = Array.CreateInstance(el, r.Length);
                    for (int i = 0; i < r.Length; i++)
                    {
                        m.SetValue(ParseByType(el, r[i], info), i);
                    }

                    parseCache.Add(rid, m);
                    return m;
                }
                else if (type == typeof(int))
                {
                    var m = int.Parse(param);
                    parseCache.Add(rid, m);
                    return m;
                }
                else if (type == typeof(float))
                {
                    var m = float.Parse(param);
                    parseCache.Add(rid, m);
                    return m;
                }
                else if (type == typeof(bool))
                {
                    var m = bool.Parse(param);
                    parseCache.Add(rid, m);
                    return m;
                }
                else if (type == typeof(string))
                {
                    var m = param;
                    parseCache.Add(rid, m);
                    return m;
                }
                else if (type.IsEnum)
                {
                    return Enum.Parse(type, param);
                }
            }
            catch (Exception e)
            {
                Log.Error($"[{info}]无法将：{param}尝试转换为{type}");
                Debug.LogException(e);
            }

            return null;
        }

        /// <summary>
        /// 卸载当前场景
        /// </summary>
        public static async UniTask UnloadCurrentSceneAsync()
        {
            if (_currentScene == "ChangeScene_1")
                _currentScene = "ChangeScene_2";
            else
                _currentScene = "ChangeScene_1";

            await SceneManager.LoadSceneAsync(_currentScene, LoadSceneMode.Single);

            await Resources.UnloadUnusedAssets();
            RenderSettings.fog = true;
            RenderSettings.fogColor = Color.black;
            RenderSettings.fogDensity = 0.08f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
        }


        public static TimeSpan ToTimeSpan(this float d) => TimeSpan.FromSeconds(d);


        public static T TryGet<T>(this T[] str, int index, T defaultValue = default)
        {
            if (str == null || index >= str.Length)
                return defaultValue;

            return str[index];
        }

        public static Vector2Int ToHardDirection(this Vector2 dir)
        {
            float up = 0, down = 0, left = 0, right = 0;

            if (dir.y > 0)
                up = dir.y;
            else if (dir.y < 0)
                down = -dir.y;

            if (dir.x > 0)
                right = dir.x;
            else if (dir.x < 0)
                left = -dir.x;


            if (up > down && up > left - 0.2f && up > right - 0.2f)
                return Vector2Int.up;
            if (down > up && down > left - 0.2f && down > right - 0.2f)
                return Vector2Int.down;
            if (left > up && left > down && left > right)
                return Vector2Int.left;
            if (right > up && right > down && right > left)
                return Vector2Int.right;

            return Vector2Int.zero;
        }

        public static bool TryParseBool(this string str, bool defaultValue = default)
        {
            if (bool.TryParse(str, out var p))
                return p;

            return defaultValue;
        }

        public static float TryParseFloat(this string str, float defaultValue = default)
        {
            if (float.TryParse(str, out var p))
                return p;

            return defaultValue;
        }

        public static int TryParseInt(this string str, int defaultValue = default)
        {
            if (int.TryParse(str, out var p))
                return p;

            return defaultValue;
        }

        public static T TryParseEnum<T>(this string str, T defaultValue = default) where T : struct, Enum
        {
            if (Enum.TryParse<T>(str, out var p))
            {
                return p;
            }

            return defaultValue;
        }


        public static T GetComponentInChildrenExt<T>(this Component self, ref T cacheField,
            bool includeInactive = false)
        {
            if (self == null)
                return default;
            if (cacheField == null || cacheField.Equals(null))
                cacheField = self.GetComponentInChildren<T>(includeInactive);
            return cacheField;
        }

        public static T GetComponentExt<T>(this Component self, ref T cacheField)
        {
            if (self == null)
                return default;
            if (cacheField == null || cacheField.Equals(null))
                cacheField = self.GetComponent<T>();
            return cacheField;
        }

        /// <summary>
        /// 计算概率范围0-1
        /// </summary>
        /// <param name="chance"></param>
        /// <returns></returns>
        public static bool RandomChance01(float chance)
        {
            if (chance <= 0f)
                return false;
            if (chance >= 1f)
                return true;
            var v = Random.Range(0f, 1f);
            return chance >= v;
        }

        /// <summary>
        /// 计算碰撞点的角度
        /// </summary>
        /// <param name="selectBounds"></param>
        /// <param name="hitPoint"></param>
        /// <returns></returns>
        public static float CalcHitPointAngle(Bounds selectBounds, Vector3 hitPoint)
        {
            var dir = (hitPoint - selectBounds.center).normalized;
            var angle = Vector2.SignedAngle(dir, Vector2.up);
            return angle;
        }

        /// <summary>
        /// 计算碰撞点
        /// </summary>
        /// <param name="selectBounds"></param>
        /// <param name="targetBounds"></param>
        /// <param name="tendencyX"></param>
        /// <param name="tendencyY"></param>
        /// <returns></returns>
        public static Vector3 CalcHitPoint(Bounds selectBounds, Bounds targetBounds, int tendencyX = 0,
            int tendencyY = 0)
        {
            Vector3 result = Vector3.zero;

            //归一化趋势
            Vector2 tendency = new Vector2(tendencyX + 1f, tendencyY + 1f) * 0.5f;

            #region 计算X

            Vector2 selectRangeWidth = new Vector2(selectBounds.min.x, selectBounds.max.x);
            Vector2 targetRangeWidth = new Vector2(targetBounds.min.x, targetBounds.max.x);

            Vector2 width = Vector2.zero;

            //目标完全包围自己
            if (targetRangeWidth.x <= selectRangeWidth.x && targetRangeWidth.y >= selectRangeWidth.y)
            {
                width = selectRangeWidth;
            }
            //左
            else if (targetRangeWidth.x >= selectRangeWidth.x && targetRangeWidth.y >= selectRangeWidth.y)
            {
                width.x = targetRangeWidth.x;
                width.y = selectRangeWidth.y;
            }
            //右
            else if (targetRangeWidth.x <= selectRangeWidth.x && targetRangeWidth.y <= selectRangeWidth.y)
            {
                width.x = selectRangeWidth.x;
                width.y = targetRangeWidth.y;
            }
            //自己包围目标
            else if (selectRangeWidth.x <= targetRangeWidth.x && selectRangeWidth.y >= targetRangeWidth.y)
            {
                width = targetRangeWidth;
            }
            else
            {
                width = selectRangeWidth;
            }

            result.x = Mathf.Lerp(width.x, width.y, tendency.x);

            #endregion

            #region 计算Y

            Vector2 height = Vector2.zero;
            Vector2 selectRangeHeight = new Vector2(selectBounds.min.y, selectBounds.max.y);
            Vector2 targetRangeHeight = new Vector2(targetBounds.min.y, targetBounds.max.y);


            //目标完全包围自己
            if (targetRangeHeight.x <= selectRangeHeight.x && targetRangeHeight.y >= selectRangeHeight.y)
            {
                height = selectRangeHeight;
            }
            else if (targetRangeHeight.x >= selectRangeHeight.x && targetRangeHeight.y >= selectRangeHeight.y)
            {
                height.x = targetRangeHeight.x;
                height.y = selectRangeHeight.y;
            }
            else if (targetRangeHeight.x <= selectRangeHeight.x && targetRangeHeight.y <= selectRangeHeight.y)
            {
                height.x = selectRangeHeight.x;
                height.y = targetRangeHeight.y;
            }
            else if (selectRangeHeight.x <= targetRangeHeight.x && selectRangeHeight.y >= targetRangeHeight.y)
            {
                height = targetRangeHeight;
            }
            else
            {
                height = selectRangeHeight;
            }

            result.y = Mathf.Lerp(height.x, height.y, tendency.y);

            #endregion

            // Vector2 selectRangeThickness = new Vector2(selectBounds.min.z, selectBounds.max.z);
            Vector2 targetRangeThickness = new Vector2(targetBounds.min.z, targetBounds.max.z);

            result.z = selectBounds.center.z;
            if (result.z > targetRangeThickness.y)
            {
                result.z = targetRangeThickness.y;
            }
            else if (result.z < targetRangeThickness.x)
            {
                result.z = targetRangeThickness.x;
            }


            return result;
        }


        public static T[] GetWithFilter<T>(this T[] array, Func<T, bool> filter)
        {
            List<T> result = new List<T>();
            for (int i = 0; i < array.Length; i++)
            {
                if (!filter(array[i]))
                    result.Add(array[i]);
            }

            return result.ToArray();
        }

        // public static DirectionEnum[] GetDirections(this AreaData[] areas)
        // {
        //     List<DirectionEnum> result = new List<DirectionEnum>();
        //     foreach (var variable in areas)
        //     {
        //         if (!result.Contains(variable.direction))
        //         {
        //             result.Add(variable.direction);
        //         }
        //     }
        //
        //     return result.ToArray();
        // }

        /// <summary>
        /// 按公式 "x/(x+y)" 转为百分比 范围(-1到1)
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static float FormulaNormalize(this int x, float y = 100)
        {
            return FormulaNormalize((float)x, y);
        }

        /// <summary>
        /// 将百分比
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static string ToPercentText(this float p)
        {
            return Mathf.FloorToInt(p * 100f).ToString();
        }

        /// <summary>
        /// 按公式 "x/(x+y)" 转为百分比 范围(-1到1)
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static float FormulaNormalize(this float x, float y = 100)
        {
            if (x == 0)
                return 0;

            if (x < 0)
            {
                x *= -1;
                return -(x / (x + y));
            }

            return x / (x + y);
        }

        /// <summary>
        /// 限制范围 -1到1
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static float Clamp11(this float x)
        {
            if (x < -1)
                return -1;
            if (x > 1)
                return 1;
            return x;
        }

        public static T[] GetWithFilter<T>(this List<T> list, Func<T, bool> filter)
        {
            List<T> result = new List<T>();
            for (int i = 0; i < list.Count; i++)
            {
                if (!filter(list[i]))
                {
                    result.Add(list[i]);
                }
            }

            return result.ToArray();
        }

        public static GameObject CreateGameObject(string name, Transform parent, bool single = false)
        {
            if (single)
            {
                var p = parent.Find(name);
                if (p) return p.gameObject;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localScale = Vector3.one;
            go.transform.localPosition = Vector3.zero;
            return go;
        }


        public static Vector2 Rotate(Vector2 dir, float ang)
        {
            ang *= Mathf.Deg2Rad;
            var r = Vector2.zero;
            r.x = Mathf.Cos(ang) * dir.x + Mathf.Sin(ang) * dir.y;
            r.y = -Mathf.Sin(ang) * dir.x + Mathf.Cos(ang) * dir.y;
            return r;
        }

        /// <summary>
        /// 设置为房间区域大小
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="rect"></param>
        /// <param name="top"></param>
        /// <param name="bottom"></param>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="scale"></param>
        public static void SetAsRoomAreaSize(this Transform transform, Rect rect, float top, float bottom, float left,
            float right, Vector2 scale)
        {
            var r = rect;
            transform.position = r.center + new Vector2(-left + right, +top - bottom);
            transform.localScale = new Vector3(r.size.x * scale.x + left * 2 + right * 2,
                r.size.y * scale.y + top * 2 + bottom * 2, 1);
        }

        public static Rect ToWorld(this Rect localRect, GameObject go)
        {
            var r = localRect;
            r.position += (Vector2)go.transform.position;
            return r;
        }

        public static Bounds ToLocalBounds(this Rect s)
        {
            Bounds b = new Bounds();
            b.center = new Vector3(s.center.x, 0, s.center.y);
            b.size = new Vector3(s.size.x, 10, s.size.y);
            return b;
        }

        /// <summary>
        /// 设置本地坐标的Bounds
        /// </summary>
        /// <param name="s"></param>
        /// <param name="bounds"></param>
        public static void SetLocalBounds(this BoxCollider s, Bounds bounds)
        {
            s.center = bounds.center;
            s.size = bounds.size;
        }

        /// <summary>
        /// 获取本地坐标的Bounds
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static Bounds GetLocalBounds(this BoxCollider s)
        {
            var bounds = new Bounds();
            bounds.center = s.center;
            bounds.size = s.size;
            return bounds;
        }

        public static Rect ToWorld(this Rect localRect, MonoBehaviour mo)
        {
            if (mo == null)
                return localRect;

            var r = localRect;
            r.position += (Vector2)mo.transform.position;
            return r;
        }


        /// <summary>
        /// 获取倒数
        /// 通常速度是2代表2倍速,但总时间需要获取倒数去乘
        /// deltaTime可以直接乘速度
        /// </summary>
        /// <param name="scale"></param>
        /// <returns></returns>
        public static float GetReciprocal(this float scale)
        {
            if (scale == 0)
                return 0;

            return 1f / scale;
        }

        private static Dictionary<Object, Sprite> iconCache { get; } = new Dictionary<Object, Sprite>();


        /// <summary>
        /// 实例化一个Unity Object对象
        /// </summary>
        /// <param name="obj"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Instantiate<T>(this T obj) where T : Object
        {
            if (obj == null)
                return null;

            return Object.Instantiate(obj);
        }

        /// <summary>
        /// 使用unity json方式克隆一个非Unity Object的对象
        /// </summary>
        /// <param name="obj"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T UnityClone<T>(this T obj)
        {
            var m = JsonUtility.ToJson(obj);
            return (T)JsonUtility.FromJson(m, obj.GetType());
        }

        public static T Clone<T>(this T obj)
        {
            try
            {
                var data = SerializationUtility.SerializeValue(obj, DataFormat.Binary);
                var clone = SerializationUtility.DeserializeValue<T>(data, DataFormat.Binary);
                return clone;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return default;
            }
        }


        /// <summary>
        /// a = 当前总概率
        /// b = 当前概率
        /// 如果a + b >= 1 则b = a + b - 1
        /// 如果end为true 并且概率少于1 则b = 1 - (a + 1)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="isFirst"></param>
        /// <param name="isEnd"></param>
        public static void CalcRate(ref float a, ref float b, bool isFirst, bool isEnd)
        {
            a = Mathf.Clamp01(a);
            b = Mathf.Clamp01(b);
            if (a >= 1)
            {
                if (isFirst)
                    b = 1;
                else
                    b = 0;

                return;
            }

            if (a + b < 1)
            {
                //合并概率
                if (isEnd)
                    b = 1 - a;
                else
                {
                    a += b;
                }
            }
            else
            {
                b = 1 - a;
                a = 1;
            }
        }


        /// <summary>
        /// 从列表中获取最后一个Id+1
        /// </summary>
        /// <param name="list"></param>
        /// <param name="idGetter"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static uint GetNextUId<T>(this List<T> list, Func<T, uint> idGetter)
        {
            uint id = 1;
            foreach (var v in list)
            {
                var cid = idGetter(v);
                if (id <= cid)
                    id = cid + 1;
            }

            return id;
        }

        public static int GetNextId<T>(this List<T> list, Func<T, int> idGetter)
        {
            int id = 1;
            foreach (var v in list)
            {
                var cid = idGetter(v);
                if (id <= cid)
                    id = cid + 1;
            }

            return id;
        }

        public static string AssetPathToResourcesPath(this string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return assetPath;


            assetPath = assetPath.Replace("\\", "/");

            if (!assetPath.StartsWith("Assets/Resources/"))
                return null;

            assetPath = assetPath.Replace("Assets/Resources/", "");
            if (Path.HasExtension(assetPath))
            {
                assetPath = assetPath.Replace(Path.GetExtension(assetPath), "");
            }

            return assetPath;
        }

        public static bool TryGetInterface<T>(this Object o, out T r)
        {
            if (o == null)
            {
                r = default;
                return false;
            }

            if (o is T register)
            {
                r = register;
                return true;
            }
            else if (o is GameObject go)
            {
                if (go.TryGetComponent<T>(out var reg))
                {
                    r = reg;
                    return true;
                }
            }

            r = default;
            return false;
        }

        public static int Div(this int a, int b, int defaultValue = 0)
        {
            if (b == 0 || a == 0)
                return defaultValue;

            return a / b;
        }

        public static float Div(this float a, float b, float defaultValue = 0)
        {
            if (b == 0 || a == 0)
                return defaultValue;

            return a / b;
        }

        /// <summary>
        /// 字符串参数切割
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static Dictionary<double, int> ParameterCutting(string str)
        {
            Dictionary<double, int> paramsDic = new Dictionary<double, int>();
            var parts = str.Split("|");
            if (parts.Length > 1)
            {
                foreach (var part in parts)
                {
                    var numbers = part.Split(',');

                    double.TryParse(numbers[0].Trim(), out var first);
                    int.TryParse(numbers[1].Trim(), out var second);
                    paramsDic.TryAdd(first, second);
                }
            }
            else
            {
                int.TryParse(parts[0].Trim(), out var first);
                paramsDic.TryAdd(1, first);
            }

            return paramsDic;
        }

        /// <summary>
        /// 如果接近整数则返回整数,否则返回四舍五入
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int RoundToInt(this float value)
        {
            // 这里的0.0001f可以根据需要调整误差范围
            if (Mathf.Abs(value - (int)value) <= 0.001f)
            {
                return (int)value;
            }

            return Mathf.RoundToInt(value);
        }

        public static int ToInt(this float value)
        {
            if (value < 0)
                return (int)(value - 0.001f);

            return (int)(value + 0.001f);
        }

        public static int ToFloorInt(this float value)
        {
            return Mathf.FloorToInt(value + 0.001f);
        }

        /// <summary>
        /// 如果接近整数则返回整数,否则返回向上取整
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int CeilToInt(this float value)
        {
            // 这里的0.0001f可以根据需要调整误差范围
            if (Mathf.Abs(value - (int)value) <= 0.001f)
            {
                return (int)value;
            }

            return Mathf.CeilToInt(value);
        }

        /// <summary>
        /// 解析字符串数据转化为一对Int数据，例如(1,42)(43,126) -> 1 42 43 126
        /// </summary>
        /// <param name="tupleStr"></param>
        /// <returns>转化的字符串数字列表</returns>
        public static List<int> TryParseTuple(string tupleStr)
        {
            var numbers = Regex.Matches(tupleStr, @"\d+").Cast<Match>().Select(m => int.Parse(m.Value)).ToList();
            return numbers;
        }

    }

    public static class MathTools
    {
        public static int WeightRandom<T>(this IList<T> list, Func<T, int> getter)
        {
            foreach (var i in list)
            {
                if (getter(i) < 0) return -1;
            }

            int sum = list.Sum(getter);
            float r = Random.Range(1, sum);

            for (int i = 0; i < list.Count; i++)
            {
                var c = getter(list[i]);
                if (c == 0f) continue;
                r -= c;
                if (r <= 0) return i;
            }

            return -1;
        }

        public static int WeightRandom<T>(this IList<T> list, Func<T, float> getter)
        {
            foreach (var i in list)
            {
                if (getter(i) < 0) return -1;
            }

            float sum = list.Sum(getter);
            float r = Random.Range(0f, sum);

            for (int i = 0; i < list.Count; i++)
            {
                var c = getter(list[i]);
                if (c == 0f) continue;
                r -= c;
                if (r <= 0) return i;
            }

            return -1;
        }


        /// <summary>
        /// 加权随机
        /// </summary>
        /// <param name="list">权重列表</param>
        /// <returns>
        /// 随机结果落在哪个区间，返回列表的index(从0开始)
        /// <para>参数若不合法，会返回-1</para>
        /// </returns>
        public static int WeightRandom(this IList<int> list)
        {
            foreach (var i in list)
            {
                if (i < 0) return -1;
            }

            int sum = list.Sum();
            int r = Random.Range(1, sum + 1);

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == 0) continue;
                r -= list[i];
                if (r <= 0) return i;
            }

            return -1;
        }

        /// <summary>
        /// 加权随机
        /// </summary>
        /// <param name="list">权重列表</param>
        /// <returns>
        /// 随机结果落在哪个区间，返回列表的index(从0开始)
        /// <para>参数若不合法，会返回-1</para>
        /// </returns>
        public static int WeightRandom(this IList<float> list)
        {
            foreach (var i in list)
            {
                if (i < 0) return -1;
            }

            float sum = list.Sum();
            float r = Random.Range(0f, sum);

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == 0f) continue;
                r -= list[i];
                if (r <= 0) return i;
            }

            return -1;
        }

        //抛物线
        public static Vector2 Parabola(Vector2 start, Vector2 end, float height, float step)
        {
            float Func(float x) => 4 * (-height * x * x + height * x);

            var mid = Vector2.Lerp(start, end, step);

            return new Vector2(mid.x, Func(step) + Mathf.Lerp(start.y, end.y, step));
        }

        //二阶贝塞尔曲线
        public static Vector2 QuardaticBezier(Vector2[] points, float t)
        {
            if (points.Length < 3) return Vector2.zero;
            Vector2 a = points[0];
            Vector2 b = points[1];
            Vector2 c = points[2];

            Vector3 aa = a + (b - a) * t;
            Vector3 bb = b + (c - b) * t;
            return aa + (bb - aa) * t;
        }
    }
}