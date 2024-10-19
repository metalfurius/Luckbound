using UnityEngine;

public class Randomizer : MonoBehaviour
{
    public static Randomizer Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
    }

    public int GetRandomizedInt(float baseValue, float percentage)
    {
        var _range = baseValue * (percentage / 100f);
        var _randomOffset = Random.Range(-_range, _range);
        return Mathf.RoundToInt(baseValue + _randomOffset); 
    }
    public float GetRandomizedFloat(float baseValue, float percentage)
    {
        var _range = baseValue * (percentage / 100f);
        var _randomOffset = Random.Range(-_range, _range);
        return baseValue + _randomOffset;
    }
}