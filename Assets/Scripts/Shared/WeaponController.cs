using UnityEngine;

public abstract class WeaponController : MonoBehaviour
{
    public abstract string GetCurrentCategoryName();

    public abstract string GetCurrentWeaponName();
}
