using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WeaponAnimation
{
    public string name;
    public bool collidersExpanded; 
    public List<Sprite> sprites;
    public List<int> frameTimings;
    public List<BoxCollider2D> colliders;
    public List<float> damagePerFrame;
}