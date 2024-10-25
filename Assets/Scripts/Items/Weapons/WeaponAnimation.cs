using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WeaponAnimation
{
    public string name;
    public List<Sprite> sprites;
    public List<int> frameTimings;
    public List<Collider2D> colliders;
    public List<float> damagePerFrame;
}