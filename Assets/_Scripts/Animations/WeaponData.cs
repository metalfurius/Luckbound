using UnityEngine;


[CreateAssetMenu(fileName = "NewWeapon", menuName = "AnimData/WeaponData")]
public class WeaponData : BaseData
{
    public int baseDamage;
    public Sprite weaponIcon;
    public float attackSpeed;
}