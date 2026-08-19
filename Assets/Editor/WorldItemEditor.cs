using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WorldItem))]
public class WorldItemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

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

            EditorGUILayout.PropertyField(iterator, true);
        }

        serializedObject.ApplyModifiedProperties();

        DrawWeaponItemComponentGuidance((WorldItem)target);
        DrawAmmoItemComponentGuidance((WorldItem)target);
    }

    private static void DrawWeaponItemComponentGuidance(WorldItem worldItem)
    {
        if (!worldItem)
            return;

        if (!(worldItem.GetItemDefinition() is WeaponDefinition))
            return;

        WeaponItem weaponItem = worldItem.GetComponent<WeaponItem>();
        if (weaponItem)
            return;

        EditorGUILayout.HelpBox("Weapon world items need a WeaponItem component to store loaded magazine rounds.", MessageType.Warning);
        if (GUILayout.Button("Add WeaponItem Component"))
            Undo.AddComponent<WeaponItem>(worldItem.gameObject);
    }

    private static void DrawAmmoItemComponentGuidance(WorldItem worldItem)
    {
        if (!worldItem)
            return;

        ScriptableObject itemDefinition = worldItem.GetItemDefinition();
        if (!(itemDefinition is AmmoDefinition) && !(itemDefinition is AmmoItemDefinition))
            return;

        AmmoItem ammoItem = worldItem.GetComponent<AmmoItem>();
        if (ammoItem)
            return;

        EditorGUILayout.HelpBox("Ammo world items can use an AmmoItem component to define how many rounds the pickup contains.", MessageType.Info);
        if (GUILayout.Button("Add AmmoItem Component"))
            Undo.AddComponent<AmmoItem>(worldItem.gameObject);
    }
}
