using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerState), true)]
[CanEditMultipleObjects]
public class PlayerStateEditor : Editor
{
    private const string ProgressionPropertyName = "progression";
    private const string LevelPropertyName = "level";
    private const string ExperiencePropertyName = "experience";

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

            if (iterator.propertyPath == ProgressionPropertyName)
            {
                DrawProgressionProperty(iterator);
                if (!iterator.isExpanded)
                    DrawLevelExperienceProgress(iterator);

                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawProgressionProperty(SerializedProperty progressionProperty)
    {
        EditorGUILayout.PropertyField(progressionProperty, false);
        if (!progressionProperty.isExpanded)
            return;

        EditorGUI.indentLevel++;

        SerializedProperty childProperty = progressionProperty.Copy();
        SerializedProperty endProperty = childProperty.GetEndProperty();
        bool enterChildren = true;

        while (childProperty.NextVisible(enterChildren) && !SerializedProperty.EqualContents(childProperty, endProperty))
        {
            enterChildren = false;
            EditorGUILayout.PropertyField(childProperty, true);

            if (childProperty.name == ExperiencePropertyName)
            {
                EditorGUILayout.LabelField("Level Progress", EditorStyles.boldLabel);
                DrawLevelExperienceProgress(progressionProperty);
            }
        }

        EditorGUI.indentLevel--;
    }

    private void DrawLevelExperienceProgress(SerializedProperty progressionProperty)
    {
        PlayerState playerState = target as PlayerState;
        if (!playerState)
            return;

        if (targets.Length > 1)
        {
            EditorGUILayout.HelpBox("Level progress is shown when editing one PlayerState at a time.", MessageType.Info);
            return;
        }

        SerializedProperty levelProperty = progressionProperty.FindPropertyRelative(LevelPropertyName);
        SerializedProperty experienceProperty = progressionProperty.FindPropertyRelative(ExperiencePropertyName);

        int level = levelProperty != null ? levelProperty.intValue : playerState.GetLevel();
        int currentExperience = Mathf.Max(0, experienceProperty != null ? experienceProperty.intValue : playerState.GetExperience());
        int experienceToNextLevel = Mathf.Max(0, playerState.GetExperienceToNextLevelForLevel(level));
        int remainingExperience = Mathf.Max(0, experienceToNextLevel - currentExperience);
        float progress = experienceToNextLevel > 0
            ? Mathf.Clamp01((float)currentExperience / experienceToNextLevel)
            : 1f;

        Rect progressRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
        progressRect = EditorGUI.IndentedRect(progressRect);

        string progressLabel = experienceToNextLevel > 0
            ? currentExperience + " / " + experienceToNextLevel + " XP (" + Mathf.RoundToInt(progress * 100f) + "%)"
            : "Max level";

        EditorGUI.ProgressBar(progressRect, progress, progressLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("XP To Next Level", experienceToNextLevel);
            EditorGUILayout.IntField("XP Remaining", remainingExperience);
        }
    }
}
