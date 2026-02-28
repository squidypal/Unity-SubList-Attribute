using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Register per class: [CustomPropertyDrawer(typeof(YourClass))] public class YourClassDrawer : SubListDrawer { }
public class SubListDrawer : PropertyDrawer
{
    private static readonly Dictionary<string, bool> FoldoutStates = new();

    private const float HeaderHeight = 22f;
    private const float Padding = 2f;

    private static GUIStyle _boldFoldout;
    private static GUIStyle BoldFoldout
    {
        get
        {
            if (_boldFoldout == null)
            {
                _boldFoldout = new GUIStyle(EditorStyles.foldout)
                {
                    fontStyle = FontStyle.Bold
                };
            }
            return _boldFoldout;
        }
    }

    private struct GroupInfo
    {
        public string Name;
        public bool StartClosed;
        public List<string> FieldNames;
    }

    private List<GroupInfo> BuildGroups(SerializedProperty property)
    {
        var groups = new List<GroupInfo>();
        GroupInfo? currentGroup = null;

        var targetType = GetSerializedType(property);
        if (targetType == null)
        {
            // Fallback: ungrouped
            var fallback = new GroupInfo { Name = null, StartClosed = false, FieldNames = new List<string>() };
            var iter = property.Copy();
            var end = iter.GetEndProperty();
            if (iter.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iter, end)) break;
                    fallback.FieldNames.Add(iter.name);
                } while (iter.NextVisible(false));
            }
            groups.Add(fallback);
            return groups;
        }

        var fields = targetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var field in fields)
        {
            if (!IsFieldSerialized(field)) continue;

            var subListAttr = field.GetCustomAttribute<SubListAttribute>();
            if (subListAttr != null)
            {
                if (currentGroup.HasValue)
                    groups.Add(currentGroup.Value);

                currentGroup = new GroupInfo
                {
                    Name = subListAttr.Name,
                    StartClosed = subListAttr.StartClosed,
                    FieldNames = new List<string> { field.Name }
                };
            }
            else if (currentGroup.HasValue)
            {
                var g = currentGroup.Value;
                g.FieldNames.Add(field.Name);
                currentGroup = g;
            }
            else
            {
                var ungrouped = new GroupInfo { Name = null, StartClosed = false, FieldNames = new List<string> { field.Name } };
                groups.Add(ungrouped);
            }
        }

        if (currentGroup.HasValue)
            groups.Add(currentGroup.Value);

        return groups;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        var groups = BuildGroups(property);
        foreach (var group in groups)
        {
            if (group.Name != null)
            {
                height += HeaderHeight + Padding;

                string key = GetFoldoutKey(property, group.Name);
                if (!FoldoutStates.ContainsKey(key))
                    FoldoutStates[key] = !group.StartClosed;

                if (FoldoutStates[key])
                {
                    height += GetGroupFieldsHeight(property, group.FieldNames);
                    height += Padding;
                }
            }
            else
            {
                height += GetGroupFieldsHeight(property, group.FieldNames);
            }
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        float yOffset = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        EditorGUI.indentLevel++;

        var groups = BuildGroups(property);
        foreach (var group in groups)
        {
            if (group.Name != null)
            {
                Rect headerRect = new Rect(position.x, yOffset, position.width, HeaderHeight);
                DrawHeaderBackground(headerRect);

                string key = GetFoldoutKey(property, group.Name);
                if (!FoldoutStates.ContainsKey(key))
                    FoldoutStates[key] = !group.StartClosed;

                FoldoutStates[key] = EditorGUI.Foldout(headerRect, FoldoutStates[key], group.Name, true, BoldFoldout);
                yOffset += HeaderHeight + Padding;

                if (FoldoutStates[key])
                {
                    EditorGUI.indentLevel++;
                    yOffset = DrawGroupFields(position, property, group.FieldNames, yOffset);
                    yOffset += Padding;
                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                yOffset = DrawGroupFields(position, property, group.FieldNames, yOffset);
            }
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    private float DrawGroupFields(Rect position, SerializedProperty parentProperty, List<string> fieldNames, float yOffset)
    {
        foreach (var fieldName in fieldNames)
        {
            var childProp = parentProperty.FindPropertyRelative(fieldName);
            if (childProp == null) continue;

            float propHeight = EditorGUI.GetPropertyHeight(childProp, true);
            Rect propRect = new Rect(position.x, yOffset, position.width, propHeight);
            EditorGUI.PropertyField(propRect, childProp, true);
            yOffset += propHeight + EditorGUIUtility.standardVerticalSpacing;
        }
        return yOffset;
    }

    private float GetGroupFieldsHeight(SerializedProperty parentProperty, List<string> fieldNames)
    {
        float height = 0f;
        foreach (var fieldName in fieldNames)
        {
            var childProp = parentProperty.FindPropertyRelative(fieldName);
            if (childProp == null) continue;
            height += EditorGUI.GetPropertyHeight(childProp, true) + EditorGUIUtility.standardVerticalSpacing;
        }
        return height;
    }

    private Type GetSerializedType(SerializedProperty property)
    {
        var targetObject = property.serializedObject.targetObject;
        var type = targetObject.GetType();
        string[] pathParts = property.propertyPath.Split('.');

        for (int i = 0; i < pathParts.Length; i++)
        {
            string part = pathParts[i];

            if (part == "Array")
            {
                i++; // skip "data[n]"
                if (type.IsArray)
                    type = type.GetElementType();
                else if (type.IsGenericType)
                    type = type.GetGenericArguments()[0];
                continue;
            }

            if (part.StartsWith("data[")) continue;

            var field = type.GetField(part,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return null;
            type = field.FieldType;

            if (type.IsArray)
                type = type.GetElementType();
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                type = type.GetGenericArguments()[0];
        }

        return type;
    }

    private bool IsFieldSerialized(FieldInfo field)
    {
        if (field.IsDefined(typeof(NonSerializedAttribute))) return false;
        if (field.IsDefined(typeof(HideInInspector))) return false;
        if (field.IsPublic) return true;
        return field.IsDefined(typeof(SerializeField));
    }

    private string GetFoldoutKey(SerializedProperty property, string groupName)
    {
        return property.serializedObject.targetObject.GetInstanceID() + "." + property.propertyPath + "." + groupName;
    }

    private void DrawHeaderBackground(Rect rect)
    {
        Color bgColor = EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.22f, 0.22f)
            : new Color(0.82f, 0.82f, 0.82f);
        EditorGUI.DrawRect(rect, bgColor);

        Color lineColor = EditorGUIUtility.isProSkin
            ? new Color(0.13f, 0.13f, 0.13f)
            : new Color(0.6f, 0.6f, 0.6f);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), lineColor);
    }
}
