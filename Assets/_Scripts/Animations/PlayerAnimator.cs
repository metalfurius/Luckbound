using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimator : BaseAnimator
{
    public void PlayIdleAnimation(List<Sprite> idleSprites, float frameDuration)
    {
        RequestAnimation(new AnimationRequest
        {
            Sprites = idleSprites,
            FrameTimings = CreateUniformTimings(idleSprites.Count, frameDuration),
            Priority = 0 // Lowest priority
        });
    }
    
    public void PlayMovementAnimation(List<Sprite> movementSprites, float frameDuration)
    {
        RequestAnimation(new AnimationRequest
        {
            Sprites = movementSprites,
            FrameTimings = CreateUniformTimings(movementSprites.Count, frameDuration),
            Priority = 1 // Low priority, interruptible by attack or other animations
        });
    }

    public void PlayAttackAnimation(List<Sprite> attackSprites, List<float> frameTimings, System.Action<int> onHitFrame)
    {
        RequestAnimation(new AnimationRequest
        {
            Sprites = attackSprites,
            FrameTimings = frameTimings,
            Priority = 2, // Higher priority than movement
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