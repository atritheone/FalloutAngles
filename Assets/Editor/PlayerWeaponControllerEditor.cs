using System;
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerWeaponController))]
public class PlayerWeaponControllerEditor : Editor
{
    private const string WeaponsPropertyName = "weapons";
    private const string EquippedWeaponIndexPropertyName = "equippedWeaponIndex";
    private const string CurrentWeaponAmmoPropertyName = "currentWeaponAmmo";
    private const string WeaponNameRelativePropertyName = "WeaponName";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty weaponsProperty = serializedObject.FindProperty(WeaponsPropertyName);
        SerializedProperty equippedWeaponIndexProperty = serializedObject.FindProperty(EquippedWeaponIndexPropertyName);
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(iterator, true);

                continue;
            }

            if (iterator.propertyPath == CurrentWeaponAmmoPropertyName)
            {
                DrawCurrentWeaponAmmoField(iterator, weaponsProperty, equippedWeaponIndexProperty);
                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawCurrentWeaponAmmoField(
        SerializedProperty currentWeaponAmmoProperty,
        SerializedProperty weaponsProperty,
        SerializedProperty equippedWeaponIndexProperty)
    {
        currentWeaponAmmoProperty.intValue = Mathf.Max(0, currentWeaponAmmoProperty.intValue);
        string equippedWeaponName = ResolveEquippedWeaponName(weaponsProperty, equippedWeaponIndexProperty);

        if (!TryResolveMagazineSize(equippedWeaponName, out int magazineSize))
        {
            EditorGUILayout.PropertyField(currentWeaponAmmoProperty, true);
            return;
        }

        int clampedAmmo = Mathf.Clamp(currentWeaponAmmoProperty.intValue, 0, magazineSize);
        if (currentWeaponAmmoProperty.intValue != clampedAmmo)
            currentWeaponAmmoProperty.intValue = clampedAmmo;

        EditorGUILayout.IntSlider(currentWeaponAmmoProperty, 0, magazineSize);
    }

    private static string ResolveEquippedWeaponName(
        SerializedProperty weaponsProperty,
        SerializedProperty equippedWeaponIndexProperty)
    {
        if (weaponsProperty == null || equippedWeaponIndexProperty == null)
            return string.Empty;

        int equippedWeaponIndex = equippedWeaponIndexProperty.intValue;
        if (!weaponsProperty.isArray || equippedWeaponIndex < 0 || equippedWeaponIndex >= weaponsProperty.arraySize)
            return string.Empty;

        SerializedProperty equippedWeaponProperty = weaponsProperty.GetArrayElementAtIndex(equippedWeaponIndex);
        if (equippedWeaponProperty == null)
            return string.Empty;

        SerializedProperty weaponNameProperty = equippedWeaponProperty.FindPropertyRelative(WeaponNameRelativePropertyName);
        if (weaponNameProperty == null)
            return string.Empty;

        return weaponNameProperty.stringValue;
    }

    private static bool TryResolveMagazineSize(string weaponName, out int magazineSize)
    {
        magazineSize = 0;

        if (string.IsNullOrWhiteSpace(weaponName))
            return false;

        string normalizedWeaponName = NormalizeKey(weaponName);
        string[] weaponDefinitionGuids = AssetDatabase.FindAssets("t:WeaponDefinition");

        for (int i = 0; i < weaponDefinitionGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(weaponDefinitionGuids[i]);
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(assetPath);
            if (!definition)
                continue;

            if (!DoesWeaponNameMatchDefinition(weaponName, normalizedWeaponName, definition))
                continue;

            magazineSize = Mathf.Max(0, definition.GetMagazineSize());
            return true;
        }

        return false;
    }

    private static bool DoesWeaponNameMatchDefinition(string weaponName, string normalizedWeaponName, WeaponDefinition definition)
    {
        string displayName = definition.GetDisplayName();
        string itemId = definition.GetItemId();
        string assetName = definition.name;

        if (string.Equals(weaponName, displayName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(weaponName, itemId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(weaponName, assetName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (normalizedWeaponName.Length == 0)
            return false;

        if (normalizedWeaponName == NormalizeKey(displayName))
            return true;

        if (normalizedWeaponName == NormalizeKey(itemId))
            return true;

        if (normalizedWeaponName == NormalizeKey(assetName))
            return true;

        return false;
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        StringBuilder builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
