using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAnimation", menuName = "AnimData/BaseData")]
public class BaseData : ScriptableObject
{
    public string dataName;
    public List<AnimationRequest> animations;
}