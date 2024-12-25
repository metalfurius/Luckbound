using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimator : BaseAnimator
{
    public void PlayIdleAnimation(List<Sprite> idleSprites, float frameDuration)
    {
        RequestAnimation(new AnimationRequest
        {
            sprites = idleSprites,
            frameTimings = CreateUniformTimings(idleSprites.Count, frameDuration),
            priority = 0 // Lowest priority
        });
    }
    
    public void PlayMovementAnimation(List<Sprite> movementSprites, float frameDuration)
    {
        RequestAnimation(new AnimationRequest
        {
            sprites = movementSprites,
            frameTimings = CreateUniformTimings(movementSprites.Count, frameDuration),
            priority = 1 // Low priority, interruptible by attack or other animations
        });
    }

    public void PlayAttackAnimation(List<Sprite> attackSprites, List<float> frameTimings, List<BoxCollider2D> colliders, List<float> damagePerFrame, System.Action<int> onHitFrame)
    {
        RequestAnimation(new AnimationRequest
        {
            sprites = attackSprites,
            frameTimings = frameTimings,
            priority = 2, // Higher priority than movement
            colliders = colliders, // Set colliders
            damagePerFrame = damagePerFrame, // Set damage per frame
            OnFrameUpdated = onHitFrame
        });
    }

    private List<float> CreateUniformTimings(int count, float duration)
    {
        var timings = new List<float>();
        for (int i = 0; i < count; i++) timings.Add(duration);
        return timings;
    }
}