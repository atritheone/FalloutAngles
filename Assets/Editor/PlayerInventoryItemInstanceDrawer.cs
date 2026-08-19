using System;
using UnityEditor;
using UnityEngine;

public abstract class InventoryItemInstanceDrawerBase : PropertyDrawer
{
    private const float VerticalSpacing = 2f;
    private const string ItemInstancesToken = ".itemInstances.Array.data[";
    private const string LoadedMagazineRoundsPropertyName = "loadedMagazineRounds";

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return height;

        ScriptableObject itemDefinition = ResolveItemDefinition(property);
        bool showCondition = ShouldShowCondition(itemDefinition);
        bool showLoadedRounds = ShouldShowLoadedMagazineRounds(itemDefinition);

        int visibleFieldCount = 3;
        if (showCondition)
            visibleFieldCount++;

        if (showLoadedRounds)
            visibleFieldCount++;

        height += (EditorGUIUtility.singleLineHeight + VerticalSpacing) * visibleFieldCount;
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            ScriptableObject itemDefinition = ResolveItemDefinition(property);
            bool showCondition = ShouldShowCondition(itemDefinition);
            bool showLoadedRounds = ShouldShowLoadedMagazineRounds(itemDefinition);

            EditorGUI.indentLevel++;

            Rect lineRect = new Rect(
                position.x,
                foldoutRect.yMax + VerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight);

            DrawChildProperty(ref lineRect, property, "instanceId");
            DrawChildProperty(ref lineRect, property, "quantity");

            if (showCondition)
                DrawChildProperty(ref lineRect, property, "conditionPercent");

            if (showLoadedRounds)
                DrawLoadedMagazineRoundsSlider(ref lineRect, property, itemDefinition);

            DrawChildProperty(ref lineRect, property, "value");

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    private static void DrawChildProperty(ref Rect lineRect, SerializedProperty parentProperty, string childPropertyName)
    {
        SerializedProperty childProperty = parentProperty.FindPropertyRelative(childPropertyName);
        if (childProperty != null)
            EditorGUI.PropertyField(lineRect, childProperty);

        lineRect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
    }

    private static void DrawLoadedMagazineRoundsSlider(
        ref Rect lineRect,
        SerializedProperty parentProperty,
        ScriptableObject itemDefinition)
    {
        SerializedProperty loadedMagazineRoundsProperty =
            parentProperty.FindPropertyRelative(LoadedMagazineRoundsPropertyName);

        if (loadedMagazineRoundsProperty != null)
        {
            int maxRounds = ResolveMagazineSize(itemDefinition);
            int clampedRounds = Mathf.Clamp(loadedMagazineRoundsProperty.intValue, 0, maxRounds);
            if (loadedMagazineRoundsProperty.intValue != clampedRounds)
                loadedMagazineRoundsProperty.intValue = clampedRounds;

            EditorGUI.IntSlider(lineRect, loadedMagazineRoundsProperty, 0, maxRounds);
        }

        lineRect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
    }

    private static ScriptableObject ResolveItemDefinition(SerializedProperty instanceProperty)
    {
        if (instanceProperty == null || instanceProperty.serializedObject == null)
            return null;

        string propertyPath = instanceProperty.propertyPath;
        if (string.IsNullOrEmpty(propertyPath))
            return null;

        int tokenIndex = propertyPath.IndexOf(ItemInstancesToken, StringComparison.Ordinal);
        if (tokenIndex < 0)
            return null;

        string entryPath = propertyPath.Substring(0, tokenIndex);
        SerializedProperty entryProperty = instanceProperty.serializedObject.FindProperty(entryPath);
        if (entryProperty == null)
            return null;

        SerializedProperty definitionProperty = entryProperty.FindPropertyRelative("itemDefinition");
        if (definitionProperty == null)
            return null;

        return definitionProperty.objectReferenceValue as ScriptableObject;
    }

    private static bool ShouldShowCondition(ScriptableObject itemDefinition)
    {
        return itemDefinition is WeaponDefinition || itemDefinition is ApparelDefinition;
    }

    private static bool ShouldShowLoadedMagazineRounds(ScriptableObject itemDefinition)
    {
        return ResolveMagazineSize(itemDefinition) > 0;
    }

    private static int ResolveMagazineSize(ScriptableObject itemDefinition)
    {
        if (!(itemDefinition is WeaponDefinition weaponDefinition))
            return 0;

        return Mathf.Max(0, weaponDefinition.GetMagazineSize());
    }
}

[CustomPropertyDrawer(typeof(PlayerInventory.ItemInstanceData))]
public class PlayerInventoryItemInstanceDrawer : InventoryItemInstanceDrawerBase
{
}

[CustomPropertyDrawer(typeof(NPCInventory.ItemInstanceData))]
public class NPCInventoryItemInstanceDrawer : InventoryItemInstanceDrawerBase
{
}
