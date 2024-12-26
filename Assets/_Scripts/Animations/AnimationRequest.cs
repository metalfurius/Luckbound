using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AnimationRequest
{
    public string animationName;
    public List<Sprite> sprites;
    public List<float> frameTimings;
    public int priority; // Higher priority interrupts lower priority animations
    public bool loop;
    public List<BoxCollider2D> colliders; // New field for colliders
    public List<float> damagePerFrame; // New field for damage per frame
    public Action<int> OnFrameUpdated;
    public Action OnAnimationCompleted;
}