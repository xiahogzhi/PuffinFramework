#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Puffin.Modules.GameDevKit.Runtime.Behaviours;
using Puffin.Modules.GameDevKit.Runtime.Behaviours.Attributes;
using Puffin.Modules.GameDevKit.Runtime.Behaviours.Enums;
using UnityEditor;
using UnityEngine;

namespace Puffin.Modules.GameDevKit.Editor
{
    /// <summary>
    /// GameScript 编辑器扩展，提供自动引用赋值功能
    /// </summary>
    [CustomEditor(typeof(GameScript), true)]
    public class GameScriptEditor : UnityEditor.Editor
    {
        private List<Transform> _childCache;
        private List<(FieldInfo field, RequiredAttribute attr)> _requiredFields;

        private void OnEnable()
        {
            FindReference();
        }

        public override void OnInspectorGUI()
        {
            // 显示 Required 字段警告
            if (_requiredFields != null)
            {
                foreach (var (field, attr) in _requiredFields)
                {
                    if (!IsFieldAssigned(field, target))
                    {
                        var msg = string.IsNullOrEmpty(attr.Message)
                            ? $"字段 '{field.Name}' 未赋值"
                            : attr.Message;
                        EditorGUILayout.HelpBox(msg, MessageType.Warning);
                    }
                }
            }
            base.OnInspectorGUI();
        }

        [MenuItem("CONTEXT/GameScript/Assign References")]
        private static void FindReferenceMenu(MenuCommand command)
        {
            var editor = CreateEditor(command.context) as GameScriptEditor;
            editor?.FindReference();
            DestroyImmediate(editor);
        }

        public void FindReference()
        {
            var script = target as GameScript;
            if (script == null || Application.isPlaying) return;

            var fields = GetAllFields(target.GetType());
            var isChanged = false;
            _childCache = GetAllChildren(script.transform);
            _requiredFields = new List<(FieldInfo, RequiredAttribute)>();

            foreach (var field in fields)
            {
                if (!IsSerializableField(field)) continue;

                // 收集 Required 字段
                var reqAttr = field.GetCustomAttribute<RequiredAttribute>(true);
                if (reqAttr != null)
                    _requiredFields.Add((field, reqAttr));

                if (IsFieldAssigned(field, target)) continue;

                if (TryAssignField(script, field))
                    isChanged = true;
            }

            _childCache = null;

            if (InvokeCustomReference())
                isChanged = true;

            if (isChanged)
            {
                EditorUtility.SetDirty(target);
                serializedObject.ApplyModifiedProperties();
            }
        }

        private bool InvokeCustomReference()
        {
            var method = target.GetType().GetMethod("CustomReference",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return method?.Invoke(target, null) is true;
        }

        private bool TryAssignField(GameScript script, FieldInfo field)
        {
            var getInChildrenAttr = field.GetCustomAttribute<GetInChildrenAttribute>(true);
            var getInParentAttr = field.GetCustomAttribute<GetInParentAttribute>(true);
            var anyAttr = field.GetCustomAttribute<AnyRefAttribute>(true);
            var findAttr = field.GetCustomAttribute<FindRefAttribute>(true);
            var autoCreateAttr = field.GetCustomAttribute<AutoCreateAttribute>(true);

            if (getInChildrenAttr != null)
                return TryAssignByGetInChildren(script, field, getInChildrenAttr, autoCreateAttr);
 
            if (getInParentAttr != null)
                return TryAssignByGetInParent(script, field, getInParentAttr);

            if (anyAttr != null)
                return TryAssignByAnyRef(script, field, anyAttr, autoCreateAttr);

            if (findAttr != null)
                return TryAssignByFindRef(script, field, findAttr, autoCreateAttr);

            return TryAssignByName(script, field, autoCreateAttr);
        }

        private bool TryAssignByGetInChildren(GameScript script, FieldInfo field, GetInChildrenAttribute attr, AutoCreateAttribute autoCreate)
        {
            var (isCollection, elementType) = GetCollectionInfo(field.FieldType);

            if (isCollection)
            {
                var components = attr.IncludeSelf
                    ? script.GetComponentsInChildren(elementType, true)
                    : GetComponentsInChildrenOnly(script.transform, elementType);
                return TryAssignCollection(script, field, components, elementType);
            }

            var component = attr.IncludeSelf
                ? script.GetComponentInChildren(field.FieldType, true)
                : GetComponentInChildrenOnly(script.transform, field.FieldType);

            if (component != null)
            {
                field.SetValue(script, component);
                return true;
            }

            return autoCreate != null && TryAutoCreate(script, field, script.transform);
        }

        private bool TryAssignByGetInParent(GameScript script, FieldInfo field, GetInParentAttribute attr)
        {
            var (isCollection, elementType) = GetCollectionInfo(field.FieldType);

            if (isCollection)
            {
                var components = attr.IncludeSelf
                    ? script.GetComponentsInParent(elementType, true)
                    : GetComponentsInParentOnly(script.transform, elementType);
                return TryAssignCollection(script, field, components, elementType);
            }

            var component = attr.IncludeSelf
                ? script.GetComponentInParent(field.FieldType, true)
                : GetComponentInParentOnly(script.transform, field.FieldType);

            if (component != null)
            {
                field.SetValue(script, component);
                return true;
            }
            return false;
        }

        private bool TryAssignByAnyRef(GameScript script, FieldInfo field, AnyRefAttribute attr, AutoCreateAttribute autoCreate)
        {
            if ((attr.Mode & SearchIncludeMode.Self) != 0)
            {
                if (TryAssignFromTransform(script, field, script.transform, autoCreate))
                    return true;
            }

            if ((attr.Mode & SearchIncludeMode.Child) != 0)
            {
                for (int i = 0; i < script.transform.childCount; i++)
                {
                    if (TryAssignFromTransform(script, field, script.transform.GetChild(i), autoCreate))
                        return true;
                }
            }

            return false;
        }

        private bool TryAssignByFindRef(GameScript script, FieldInfo field, FindRefAttribute attr, AutoCreateAttribute autoCreate)
        {
            var path = attr.Path;
            if (string.IsNullOrEmpty(path)) return false;

            // 检查是否包含通配符
            if (path.Contains("*") || path.Contains("?"))
                return TryAssignByPattern(script, field, path);

            var target = script.transform.Find(path);
            if (target == null) return false;

            return TryAssignFromTransform(script, field, target, autoCreate);
        }

        private bool TryAssignByPattern(GameScript script, FieldInfo field, string pattern)
        {
            var regex = WildcardToRegex(pattern);
            var (isCollection, elementType) = GetCollectionInfo(field.FieldType);
            var matches = new List<Component>();

            foreach (var child in _childCache)
            {
                var relativePath = GetRelativePath(script.transform, child);
                if (!regex.IsMatch(relativePath)) continue;

                if (isCollection)
                {
                    var comp = child.GetComponent(elementType);
                    if (comp != null) matches.Add(comp);
                }
                else
                {
                    return TryAssignFromTransform(script, field, child, null);
                }
            }

            if (isCollection && matches.Count > 0)
                return TryAssignCollection(script, field, matches.ToArray(), elementType);

            return false;
        }

        private bool TryAssignByName(GameScript script, FieldInfo field, AutoCreateAttribute autoCreate)
        {
            var fieldName = field.Name.TrimStart('_').ToLower();
            var target = FindChildByName(fieldName);
            if (target == null) return false;

            return TryAssignFromTransform(script, field, target, autoCreate);
        }

        private bool TryAssignFromTransform(GameScript script, FieldInfo field, Transform target, AutoCreateAttribute autoCreate)
        {
            if (target.gameObject.hideFlags != HideFlags.None) return false;

            var fieldType = field.FieldType;

            if (fieldType == typeof(GameObject))
            {
                field.SetValue(script, target.gameObject);
                return true;
            }

            if (typeof(Component).IsAssignableFrom(fieldType) || fieldType.IsInterface)
            {
                var component = target.GetComponent(fieldType);
                if (component != null)
                {
                    field.SetValue(script, component);
                    return true;
                }

                if (autoCreate != null && typeof(Component).IsAssignableFrom(fieldType))
                    return TryAutoCreate(script, field, target);
            }

            return false;
        }

        private bool TryAutoCreate(GameScript script, FieldInfo field, Transform target)
        {
            var fieldType = field.FieldType;
            if (!typeof(Component).IsAssignableFrom(fieldType) || fieldType.IsAbstract)
                return false;

            var component = target.gameObject.AddComponent(fieldType);
            if (component != null)
            {
                field.SetValue(script, component);
                return true;
            }
            return false;
        }

        private bool TryAssignCollection(GameScript script, FieldInfo field, Array components, Type elementType)
        {
            if (components == null || components.Length == 0) return false;

            var fieldType = field.FieldType;

            if (fieldType.IsArray)
            {
                var arr = Array.CreateInstance(elementType, components.Length);
                Array.Copy(components, arr, components.Length);
                field.SetValue(script, arr);
                return true;
            }

            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (IList)Activator.CreateInstance(fieldType);
                foreach (var comp in components)
                    list.Add(comp);
                field.SetValue(script, list);
                return true;
            }

            return false;
        }

        private (bool isCollection, Type elementType) GetCollectionInfo(Type type)
        {
            if (type.IsArray)
                return (true, type.GetElementType());

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return (true, type.GetGenericArguments()[0]);

            return (false, type);
        }

        private Component GetComponentInChildrenOnly(Transform parent, Type type)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var comp = parent.GetChild(i).GetComponentInChildren(type, true);
                if (comp != null) return comp;
            }
            return null;
        }

        private Component[] GetComponentsInChildrenOnly(Transform parent, Type type)
        {
            var list = new List<Component>();
            for (int i = 0; i < parent.childCount; i++)
                list.AddRange(parent.GetChild(i).GetComponentsInChildren(type, true));
            return list.ToArray();
        }

        private Component GetComponentInParentOnly(Transform child, Type type)
        {
            var parent = child.parent;
            return parent != null ? parent.GetComponentInParent(type, true) : null;
        }

        private Component[] GetComponentsInParentOnly(Transform child, Type type)
        {
            var parent = child.parent;
            return parent != null ? parent.GetComponentsInParent(type, true) : Array.Empty<Component>();
        }

        private Transform FindChildByName(string name)
        {
            foreach (var child in _childCache)
            {
                if (child.name.ToLower() == name)
                    return child;
            }
            return null;
        }

        private string GetRelativePath(Transform root, Transform target)
        {
            if (target == root) return "";
            var path = target.name;
            var parent = target.parent;
            while (parent != null && parent != root)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private Regex WildcardToRegex(string pattern)
        {
            var escaped = Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".");
            return new Regex("^" + escaped + "$", RegexOptions.IgnoreCase);
        }

        private static bool IsSerializableField(FieldInfo field)
        {
            return field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
        }

        private static bool IsFieldAssigned(FieldInfo field, object target)
        {
            var value = field.GetValue(target);
            if (value == null) return false;
            if (value is UnityEngine.Object obj) return obj != null;
            if (value is IList list) return list.Count > 0;
            return true;
        }

        private static List<FieldInfo> GetAllFields(Type type)
        {
            var fields = new List<FieldInfo>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            while (type != null && type != typeof(MonoBehaviour))
            {
                fields.AddRange(type.GetFields(flags));
                type = type.BaseType;
            }

            return fields;
        }

        private static List<Transform> GetAllChildren(Transform root)
        {
            var list = new List<Transform>();
            CollectChildren(root, list);
            return list;
        }

        private static void CollectChildren(Transform t, List<Transform> list)
        {
            list.Add(t);
            for (int i = 0; i < t.childCount; i++)
                CollectChildren(t.GetChild(i), list);
        }
    }
}
#endif
