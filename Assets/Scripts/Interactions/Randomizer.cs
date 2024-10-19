using UnityEngine;

public abstract class Randomizer
{
    public static int GetRandomizedInt(float baseValue, float percentage = 10f)
    {
        var _range = baseValue * (percentage / 100f);
        var _randomOffset = Random.Range(-_range, _range);
        return Mathf.RoundToInt(baseValue + _randomOffset); 
    }
    public float GetRandomizedFloat(float baseValue, float percentage = 10f)
    {
        var _range = baseValue * (percentage / 100f);
        var _randomOffset = Random.Range(-_range, _range);
        return baseValue + _randomOffset;
    }
}