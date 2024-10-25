using UnityEngine;

[CreateAssetMenu(fileName = "New Weapon", menuName = "Weapon")]
public class Weapon : ScriptableObject
{
    public string weaponName;
    public GameObject weaponGameObject;
    public WeaponAnimation[] attackAnimations;
}