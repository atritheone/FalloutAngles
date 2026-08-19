using UnityEditor;

[CustomEditor(typeof(WeaponDefinition))]
public class WeaponDefinitionEditor : Editor
{
    private const string AutomaticPropertyName = "automatic";
    private const string SpreadPropertyName = "spread";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty automaticProperty = serializedObject.FindProperty(AutomaticPropertyName);
        SerializedProperty spreadProperty = serializedObject.FindProperty(SpreadPropertyName);
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

            if (iterator.propertyPath == SpreadPropertyName)
                continue;

            EditorGUILayout.PropertyField(iterator, true);

            if (iterator.propertyPath == AutomaticPropertyName &&
                automaticProperty != null &&
                automaticProperty.boolValue &&
                spreadProperty != null)
            {
                EditorGUILayout.PropertyField(spreadProperty, true);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
