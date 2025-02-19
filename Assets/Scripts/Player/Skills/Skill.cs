using UnityEngine;

public abstract class Skill : MonoBehaviour
{
    public float cooldown = 2f;
    private float _lastUsedTime;

    private bool IsReady() => Time.time >= _lastUsedTime + cooldown;

    public virtual void Activate()
    {
        if (!IsReady()) return;
        _lastUsedTime = Time.time;
        ExecuteSkill();
    }

    protected abstract void ExecuteSkill();
}