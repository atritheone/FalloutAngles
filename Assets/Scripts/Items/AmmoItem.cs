using UnityEngine;

[RequireComponent(typeof(WorldItem))]
public class AmmoItem : MonoBehaviour
{
    // Number of rounds represented by this world ammo pickup.
    [Min(1)] [SerializeField] private int rounds = 1;

    // If true, rounds are mirrored into WorldItem quantity for pickup/inventory flows.
    [SerializeField] private bool syncWorldItemQuantity = true;

    public int GetRounds()
    {
        rounds = Mathf.Max(1, rounds);
        return rounds;
    }

    public void SetRounds(int newRounds)
    {
        rounds = Mathf.Max(1, newRounds);
        SyncWorldItemQuantity();
    }

    private void Awake()
    {
        SyncWorldItemQuantity();
    }

    private void OnValidate()
    {
        rounds = Mathf.Max(1, rounds);
        SyncWorldItemQuantity();
    }

    private void SyncWorldItemQuantity()
    {
        if (!syncWorldItemQuantity)
            return;

        WorldItem worldItem = GetComponent<WorldItem>();
        if (!worldItem)
            return;

        ScriptableObject definition = worldItem.GetItemDefinition();
        if (!(definition is AmmoDefinition) && !(definition is AmmoItemDefinition))
            return;

        worldItem.SetQuantity(rounds);
    }
}
