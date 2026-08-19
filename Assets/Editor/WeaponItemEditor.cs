using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponItem))]
public class WeaponItemEditor : Editor
{
    private const string LoadedMagazineRoundsPropertyName = "loadedMagazineRounds";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty loadedMagazineRoundsProperty = serializedObject.FindProperty(LoadedMagazineRoundsPropertyName);

        if (loadedMagazineRoundsProperty != null)
        {
            int maxRounds = ResolveMagazineSize((WeaponItem)target);
            int clampedRounds = Mathf.Clamp(loadedMagazineRoundsProperty.intValue, 0, maxRounds);
            if (loadedMagazineRoundsProperty.intValue != clampedRounds)
                loadedMagazineRoundsProperty.intValue = clampedRounds;

            EditorGUILayout.IntSlider(loadedMagazineRoundsProperty, 0, maxRounds);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static int ResolveMagazineSize(WeaponItem weaponItem)
    {
        if (!weaponItem)
            return 0;

        WorldItem worldItem = weaponItem.GetComponent<WorldItem>();
        if (!worldItem)
            return 0;

        if (!(worldItem.GetItemDefinition() is WeaponDefinition weaponDefinition))
            return 0;

        return Mathf.Max(0, weaponDefinition.GetMagazineSize());
    }
}
