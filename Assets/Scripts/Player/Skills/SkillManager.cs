using UnityEngine;

public class SkillManager : MonoBehaviour
{
    public Skill primarySkill;

    private void Update()
    {
        if (Input.GetMouseButtonDown(1)) // Right-click for example
        {
            primarySkill.Activate();
        }
    }
}