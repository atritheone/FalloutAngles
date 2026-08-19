using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NPCTestDriver))]
public class NPCTestDriverEditor : Editor
{
    private const string KillhouseLoadoutPropertyName = "killhouseLoadout";
    private const string UseSpecificWeaponPropertyName = "useSpecificKillhouseInventoryWeapon";
    private const string SelectedWeaponInstanceIdPropertyName = "selectedKillhouseInventoryWeaponInstanceId";
    private const string NpcInventoryPropertyName = "npcInventory";
    private const string KillhouseUnarmedWeaponSelectionId = "__KILLHOUSE_UNARMED__";

    private class WeaponOption
    {
        public string InstanceId;
        public string BaseLabel;
        public string Label;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty killhouseLoadoutProperty = serializedObject.FindProperty(KillhouseLoadoutPropertyName);
        SerializedProperty useSpecificWeaponProperty =
            killhouseLoadoutProperty?.FindPropertyRelative(UseSpecificWeaponPropertyName);
        SerializedProperty selectedWeaponInstanceIdProperty =
            killhouseLoadoutProperty?.FindPropertyRelative(SelectedWeaponInstanceIdPropertyName);

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

            if (iterator.propertyPath == SelectedWeaponInstanceIdPropertyName)
                continue;

            EditorGUILayout.PropertyField(iterator, true);

            if (iterator.propertyPath == KillhouseLoadoutPropertyName && iterator.isExpanded)
                DrawKillhouseInventoryWeaponDropdown(useSpecificWeaponProperty, selectedWeaponInstanceIdProperty);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawKillhouseInventoryWeaponDropdown(
        SerializedProperty useSpecificWeaponProperty,
        SerializedProperty selectedWeaponInstanceIdProperty)
    {
        if (useSpecificWeaponProperty == null ||
            selectedWeaponInstanceIdProperty == null ||
            !useSpecificWeaponProperty.boolValue)
        {
            return;
        }

        NPCInventory inventory = ResolveNpcInventory();
        List<WeaponOption> options = BuildWeaponOptions(inventory);
        if (options.Count == 0)
        {
            selectedWeaponInstanceIdProperty.stringValue = string.Empty;
            EditorGUILayout.HelpBox("This NPC inventory has no weapons to choose from.", MessageType.Info);
            return;
        }

        string selectedInstanceId = selectedWeaponInstanceIdProperty.stringValue;
        if (string.IsNullOrWhiteSpace(selectedInstanceId))
        {
            selectedWeaponInstanceIdProperty.stringValue = options[0].InstanceId ?? string.Empty;
            selectedInstanceId = selectedWeaponInstanceIdProperty.stringValue;
        }

        int selectedIndex = FindOptionIndex(options, selectedInstanceId);
        bool selectedMissing = !string.IsNullOrWhiteSpace(selectedInstanceId) && selectedIndex < 0;

        if (selectedMissing)
        {
            options.Insert(0, new WeaponOption
            {
                InstanceId = selectedInstanceId,
                BaseLabel = "Missing selected weapon",
                Label = "Missing selected weapon"
            });
            selectedIndex = 0;
        }

        string[] labels = new string[options.Count];
        for (int i = 0; i < options.Count; i++)
            labels[i] = options[i].Label;

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUILayout.Popup("Killhouse Weapon", Mathf.Max(0, selectedIndex), labels);
        if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < options.Count)
            selectedWeaponInstanceIdProperty.stringValue = options[newIndex].InstanceId ?? string.Empty;

        if (selectedMissing)
            EditorGUILayout.HelpBox("The selected killhouse weapon is no longer in this NPC's inventory.", MessageType.Warning);

        if (!inventory)
            EditorGUILayout.HelpBox("Assign or place an NPCInventory on this NPC to choose inventory weapons.", MessageType.Info);
    }

    private NPCInventory ResolveNpcInventory()
    {
        SerializedProperty inventoryProperty = serializedObject.FindProperty(NpcInventoryPropertyName);
        if (inventoryProperty != null && inventoryProperty.objectReferenceValue is NPCInventory assignedInventory)
            return assignedInventory;

        NPCTestDriver driver = target as NPCTestDriver;
        if (!driver)
            return null;

        NPCInventory inventory = driver.GetComponent<NPCInventory>();
        if (inventory)
            return inventory;

        inventory = driver.GetComponentInParent<NPCInventory>();
        return inventory ? inventory : driver.GetComponentInChildren<NPCInventory>(true);
    }

    private static List<WeaponOption> BuildWeaponOptions(NPCInventory inventory)
    {
        List<WeaponOption> options = new List<WeaponOption>
        {
            new WeaponOption
            {
                InstanceId = KillhouseUnarmedWeaponSelectionId,
                BaseLabel = "Unarmed",
                Label = "Unarmed"
            }
        };

        if (!inventory)
            return options;

        inventory.GetWeight();
        IReadOnlyList<NPCInventory.InventoryEntry> entries = inventory.GetCategoryItems(NPCInventory.InventoryCategory.Weapons);
        if (entries == null)
            return options;

        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            NPCInventory.InventoryEntry entry = entries[entryIndex];
            if (entry == null || !(entry.GetItemDefinition() is WeaponDefinition weaponDefinition))
                continue;

            IReadOnlyList<NPCInventory.ItemInstanceData> instances = entry.GetItemInstances();
            if (instances == null)
                continue;

            string baseLabel = ResolveWeaponDisplayName(weaponDefinition);
            for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
            {
                NPCInventory.ItemInstanceData instance = instances[instanceIndex];
                if (instance == null || string.IsNullOrWhiteSpace(instance.GetInstanceId()))
                    continue;

                options.Add(new WeaponOption
                {
                    InstanceId = instance.GetInstanceId(),
                    BaseLabel = baseLabel,
                    Label = baseLabel
                });
            }
        }

        DisambiguateDuplicateLabels(options);
        return options;
    }

    private static void DisambiguateDuplicateLabels(List<WeaponOption> options)
    {
        Dictionary<string, int> totalsByLabel = new Dictionary<string, int>();
        for (int i = 0; i < options.Count; i++)
        {
            string label = options[i].BaseLabel;
            totalsByLabel.TryGetValue(label, out int count);
            totalsByLabel[label] = count + 1;
        }

        Dictionary<string, int> seenByLabel = new Dictionary<string, int>();
        for (int i = 0; i < options.Count; i++)
        {
            WeaponOption option = options[i];
            string label = option.BaseLabel;
            if (!totalsByLabel.TryGetValue(label, out int total) || total <= 1)
            {
                option.Label = label;
                continue;
            }

            seenByLabel.TryGetValue(label, out int seen);
            seen++;
            seenByLabel[label] = seen;
            option.Label = label + " (" + seen + ")";
        }
    }

    private static int FindOptionIndex(List<WeaponOption> options, string selectedInstanceId)
    {
        if (string.IsNullOrWhiteSpace(selectedInstanceId))
            return 0;

        for (int i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i].InstanceId, selectedInstanceId, System.StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private static string ResolveWeaponDisplayName(WeaponDefinition weaponDefinition)
    {
        if (!weaponDefinition)
            return "Unknown Weapon";

        if (!string.IsNullOrWhiteSpace(weaponDefinition.GetDisplayName()))
            return weaponDefinition.GetDisplayName().Trim();

        if (!string.IsNullOrWhiteSpace(weaponDefinition.name))
            return weaponDefinition.name.Trim();

        return !string.IsNullOrWhiteSpace(weaponDefinition.GetItemId())
            ? weaponDefinition.GetItemId().Trim()
            : "Unknown Weapon";
    }
}
