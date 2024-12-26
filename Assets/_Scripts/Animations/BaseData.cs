using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAnimation", menuName = "AnimData/BaseData")]
public class BaseData : ScriptableObject
{
    public string dataName;
    public List<AnimationRequest> animations;
    public AnimationRequest GetAnimationByName(string animationName)
    {
        return animations.FirstOrDefault(animation => animation.animationName == animationName);
    }
}