using UnityEngine;

[RequireComponent(typeof(WorldItem))]
public class WeaponItem : MonoBehaviour
{
    // Loaded rounds currently inside this weapon's magazine.
    [Min(0)] [SerializeField] private int loadedMagazineRounds = 0;

    public int GetLoadedMagazineRounds()
    {
        // Keep serialized value clamped to current weapon definition.
        loadedMagazineRounds = ClampLoadedMagazineRounds(loadedMagazineRounds);
        return loadedMagazineRounds;
    }

    public void SetLoadedMagazineRounds(int newLoadedMagazineRounds)
    {
        // Clamp to weapon magazine capacity (or zero if no weapon definition).
        loadedMagazineRounds = ClampLoadedMagazineRounds(newLoadedMagazineRounds);
    }

    public int GetMagazineSize()
    {
        WorldItem worldItem = GetComponent<WorldItem>();
        if (!worldItem) return 0;

        if (!(worldItem.GetItemDefinition() is WeaponDefinition weaponDefinition))
            return 0;

        return Mathf.Max(0, weaponDefinition.GetMagazineSize());
    }

    private void OnValidate()
    {
        loadedMagazineRounds = ClampLoadedMagazineRounds(loadedMagazineRounds);
    }

    private int ClampLoadedMagazineRounds(int rounds)
    {
        int magazineSize = GetMagazineSize();
        return Mathf.Clamp(rounds, 0, magazineSize);
    }
}
