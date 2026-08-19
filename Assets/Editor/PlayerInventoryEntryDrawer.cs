using System;
using UnityEditor;
using UnityEngine;

public abstract class InventoryEntryDrawerBase : PropertyDrawer
{
    private const float VerticalSpacing = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return height;

        SerializedProperty itemInstancesProperty = property.FindPropertyRelative("itemInstances");
        float instancesHeight = itemInstancesProperty != null
            ? EditorGUI.GetPropertyHeight(itemInstancesProperty, true)
            : EditorGUIUtility.singleLineHeight;

        height += VerticalSpacing + EditorGUIUtility.singleLineHeight;
        height += VerticalSpacing + instancesHeight;
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            SerializedProperty itemDefinitionProperty = property.FindPropertyRelative("itemDefinition");
            SerializedProperty itemInstancesProperty = property.FindPropertyRelative("itemInstances");

            EditorGUI.indentLevel++;

            Rect definitionRect = new Rect(
                position.x,
                foldoutRect.yMax + VerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight);

            DrawDefinitionField(definitionRect, property.propertyPath, itemDefinitionProperty);

            float instancesHeight = itemInstancesProperty != null
                ? EditorGUI.GetPropertyHeight(itemInstancesProperty, true)
                : EditorGUIUtility.singleLineHeight;

            Rect instancesRect = new Rect(
                position.x,
                definitionRect.yMax + VerticalSpacing,
                position.width,
                instancesHeight);

            if (itemInstancesProperty != null)
                EditorGUI.PropertyField(instancesRect, itemInstancesProperty, true);

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    private static void DrawDefinitionField(Rect rect, string propertyPath, SerializedProperty itemDefinitionProperty)
    {
        if (itemDefinitionProperty == null)
            return;

        Type allowedType = GetDefinitionTypeForCategoryList(propertyPath);
        if (allowedType == null)
        {
            EditorGUI.PropertyField(rect, itemDefinitionProperty);
            return;
        }

        UnityEngine.Object selectedDefinition = EditorGUI.ObjectField(
            rect,
            itemDefinitionProperty.displayName,
            itemDefinitionProperty.objectReferenceValue,
            allowedType,
            false);

        if (selectedDefinition != itemDefinitionProperty.objectReferenceValue)
            itemDefinitionProperty.objectReferenceValue = selectedDefinition;
    }

    private static Type GetDefinitionTypeForCategoryList(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
            return null;

        const string listToken = ".Array.data[";
        int tokenIndex = propertyPath.LastIndexOf(listToken, StringComparison.Ordinal);
        if (tokenIndex <= 0)
            return null;

        string listPath = propertyPath.Substring(0, tokenIndex);
        int listNameStartIndex = listPath.LastIndexOf('.') + 1;
        string listName = listPath.Substring(listNameStartIndex);

        if (listName == "weapons") return typeof(WeaponDefinition);
        if (listName == "apparel") return typeof(ApparelDefinition);
        if (listName == "aid") return typeof(AidDefinition);
        if (listName == "misc") return typeof(MiscDefinition);
        if (listName == "ammo") return typeof(AmmoDefinition);

        return null;
    }
}

[CustomPropertyDrawer(typeof(PlayerInventory.InventoryEntry))]
public class PlayerInventoryEntryDrawer : InventoryEntryDrawerBase
{
}

[CustomPropertyDrawer(typeof(NPCInventory.InventoryEntry))]
public class NPCInventoryEntryDrawer : InventoryEntryDrawerBase
{
}
