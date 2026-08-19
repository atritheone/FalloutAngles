using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public static class ItemDatabaseBuilder
{
    private const string ResourcesFolderPath = "Assets/Resources";
    private const string DatabaseAssetPath = "Assets/Resources/ItemDatabase.asset";
    private const string WorldSuffix = "World";

    [DidReloadScripts]
    private static void RebuildAfterScriptsReload()
    {
        EditorApplication.delayCall += RebuildDatabase;
    }

    [MenuItem("Tools/Fallout Angles/Rebuild Item Database")]
    public static void RebuildDatabase()
    {
        EnsureResourcesFolder();

        ItemDatabase database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabaseAssetPath);
        if (!database)
        {
            database = ScriptableObject.CreateInstance<ItemDatabase>();
            AssetDatabase.CreateAsset(database, DatabaseAssetPath);
        }

        List<ItemDatabase.Entry> entries = BuildEntries(out int skippedModelPrefabs, out int skippedWithoutWorldItem, out int skippedWithoutDefinition);
        database.SetEntriesForEditor(entries);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "ItemDatabase rebuilt: " + entries.Count + " item prefabs indexed. " +
            "Skipped " + skippedModelPrefabs + " model-only prefab assets, " +
            skippedWithoutWorldItem + " prefabs without root WorldItem, and " +
            skippedWithoutDefinition + " prefabs without item definitions.");
    }

    private static List<ItemDatabase.Entry> BuildEntries(out int skippedModelPrefabs, out int skippedWithoutWorldItem, out int skippedWithoutDefinition)
    {
        List<ItemDatabase.Entry> entries = new List<ItemDatabase.Entry>();
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
        skippedModelPrefabs = 0;
        skippedWithoutWorldItem = 0;
        skippedWithoutDefinition = 0;

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!prefab)
                continue;

            PrefabAssetType prefabAssetType = PrefabUtility.GetPrefabAssetType(prefab);
            if (prefabAssetType == PrefabAssetType.Model || prefabAssetType == PrefabAssetType.NotAPrefab)
            {
                skippedModelPrefabs++;
                continue;
            }

            WorldItem worldItem = prefab.GetComponent<WorldItem>();
            if (!worldItem)
            {
                skippedWithoutWorldItem++;
                continue;
            }

            if (!worldItem.GetItemDefinition())
            {
                skippedWithoutDefinition++;
                continue;
            }

            string canonicalName = ResolveCanonicalName(prefab, worldItem);
            string[] aliases = BuildAliases(prefab, worldItem, canonicalName);
            entries.Add(new ItemDatabase.Entry(canonicalName, prefab, aliases));
        }

        entries.Sort((left, right) => string.Compare(left.ItemName, right.ItemName, StringComparison.OrdinalIgnoreCase));
        return entries;
    }

    private static string ResolveCanonicalName(GameObject prefab, WorldItem worldItem)
    {
        // The database entry represents the item pickup prefab, not the render/model prefab.
        // Example: Prefabs/Weapons/Submachine Gun/SubmachineGunWorld.prefab -> SubmachineGun.
        return StripWorldSuffix(prefab.name);
    }

    private static string[] BuildAliases(GameObject prefab, WorldItem worldItem, string canonicalName)
    {
        HashSet<string> aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddAlias(aliases, canonicalName);
        AddAlias(aliases, prefab.name);
        AddAlias(aliases, StripWorldSuffix(prefab.name));
        AddAlias(aliases, worldItem.GetItemId());
        AddAlias(aliases, worldItem.GetDisplayName());

        ScriptableObject definition = worldItem.GetItemDefinition();
        if (definition)
            AddAlias(aliases, definition.name);

        string[] result = new string[aliases.Count];
        aliases.CopyTo(result);
        return result;
    }

    private static void AddAlias(HashSet<string> aliases, string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return;

        aliases.Add(alias.Trim());
    }

    private static string StripWorldSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.EndsWith(WorldSuffix, StringComparison.OrdinalIgnoreCase)
            ? value.Substring(0, value.Length - WorldSuffix.Length)
            : value;
    }

    private static void EnsureResourcesFolder()
    {
        if (!AssetDatabase.IsValidFolder(ResourcesFolderPath))
            AssetDatabase.CreateFolder("Assets", "Resources");
    }
}
