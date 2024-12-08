using System.Collections.Generic;
using UnityEngine;

public class EnemyAnimator : BaseAnimator
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

    public void PlayAttackAnimation(List<Sprite> attackSprites, List<float> frameTimings, System.Action<int> onAttackFrame)
    {
        RequestAnimation(new AnimationRequest
        {
            Sprites = attackSprites,
            FrameTimings = frameTimings,
            Priority = 2, // Attack animations have higher priority
            OnFrameUpdated = onAttackFrame
        });
    }

    public void PlayDeathAnimation(List<Sprite> deathSprites, float frameDuration)
    {
        RequestAnimation(new AnimationRequest
        {
            Sprites = deathSprites,
            FrameTimings = CreateUniformTimings(deathSprites.Count, frameDuration),
            Priority = 3 // Highest priority (interrupts everything)
        });
    }

    private List<float> CreateUniformTimings(int count, float duration)
    {
        var timings = new List<float>();
        for (int i = 0; i < count; i++) timings.Add(duration);
        return timings;
    }
}