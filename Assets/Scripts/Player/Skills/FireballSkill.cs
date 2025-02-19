using UnityEngine;

public class FireballSkill : Skill
{
    public GameObject fireballPrefab;
    public Transform spawnPoint;

    protected override void ExecuteSkill()
    {
        Instantiate(fireballPrefab, spawnPoint.position, spawnPoint.rotation);
    }
}