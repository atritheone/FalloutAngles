using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Fallout Angles/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [SerializeField] private string itemName;
        [FormerlySerializedAs("worldPrefab")]
        [SerializeField] private GameObject itemPrefab;
        [SerializeField] private string[] aliases;

        public string ItemName => itemName;
        public GameObject ItemPrefab => itemPrefab;
        public IReadOnlyList<string> Aliases => aliases;

        public Entry(string itemName, GameObject itemPrefab, string[] aliases)
        {
            this.itemName = itemName;
            this.itemPrefab = itemPrefab;
            this.aliases = aliases ?? Array.Empty<string>();
        }
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private readonly Dictionary<string, GameObject> itemPrefabByName = new Dictionary<string, GameObject>();
    private bool lookupBuilt;

    public bool TryGetItemPrefab(string itemName, out GameObject itemPrefab)
    {
        BuildLookupIfNeeded();

        string normalizedName = NormalizeName(itemName);
        if (string.IsNullOrEmpty(normalizedName))
        {
            itemPrefab = null;
            return false;
        }

        return itemPrefabByName.TryGetValue(normalizedName, out itemPrefab) && itemPrefab != null;
    }

    public bool TryGetWorldPrefab(string itemName, out GameObject worldPrefab)
    {
        return TryGetItemPrefab(itemName, out worldPrefab);
    }

    public static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] buffer = new char[value.Length];
        int count = 0;

        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (char.IsWhiteSpace(current) || current == '_' || current == '-')
                continue;

            buffer[count] = char.ToLowerInvariant(current);
            count++;
        }

        return new string(buffer, 0, count);
    }

#if UNITY_EDITOR
    public void SetEntriesForEditor(List<Entry> newEntries)
    {
        entries = newEntries ?? new List<Entry>();
        lookupBuilt = false;
        itemPrefabByName.Clear();
    }
#endif

    private void OnValidate()
    {
        lookupBuilt = false;
        itemPrefabByName.Clear();
    }

    private void BuildLookupIfNeeded()
    {
        if (lookupBuilt)
            return;

        itemPrefabByName.Clear();

        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry == null || entry.ItemPrefab == null)
                    continue;

                RegisterAlias(entry.ItemName, entry.ItemPrefab);

                IReadOnlyList<string> aliases = entry.Aliases;
                if (aliases == null)
                    continue;

                for (int aliasIndex = 0; aliasIndex < aliases.Count; aliasIndex++)
                    RegisterAlias(aliases[aliasIndex], entry.ItemPrefab);
            }
        }

        lookupBuilt = true;
    }

    private void RegisterAlias(string alias, GameObject itemPrefab)
    {
        string normalizedAlias = NormalizeName(alias);
        if (string.IsNullOrEmpty(normalizedAlias) || itemPrefab == null)
            return;

        if (!itemPrefabByName.ContainsKey(normalizedAlias))
            itemPrefabByName.Add(normalizedAlias, itemPrefab);
    }
}
