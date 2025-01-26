using _Scripts.Animations;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    [SerializeField] private WeaponData weaponData;
    private WeaponAttackHandler _attackHandler;

    public void Initialize(WeaponAttackHandler attackHandler)
    {
        _attackHandler = attackHandler;
    }

    public void PerformAttack(BaseAnimator animator)
    {
        foreach (var animationRequest in weaponData.animations)
        {
            SetupAnimationCallbacks(animationRequest);
            animator.PlayAnimation(animationRequest);
        }
    }

    private void SetupAnimationCallbacks(AnimationRequest request)
    {
        request.OnFrameUpdated = (frameIndex) => 
        {
            if (frameIndex < request.damageFrames.Length && request.damageFrames[frameIndex])
            {
                _attackHandler.StartAttackFrame(request.damagePerFrame[frameIndex]);
            }
            else
            {
                _attackHandler.EndAttackFrame();
            }
        };

        request.OnAnimationCompleted = () => _attackHandler.EndAttackFrame();
    }
}