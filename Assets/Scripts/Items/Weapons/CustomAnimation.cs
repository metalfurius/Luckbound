using System.Collections.Generic;
using UnityEngine;

public class CustomAnimation : MonoBehaviour
{
    public List<Sprite> sprites;
    public List<int> frameTimings;
    private int _currentFrame;
    private int _frameCounter;
    private SpriteRenderer _spriteRenderer;

    private void Start()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _currentFrame = 0;
        _frameCounter = 0;
    }

    private void Update()
    {
        if (_frameCounter >= frameTimings[_currentFrame])
        {
            _currentFrame = (_currentFrame + 1) % sprites.Count;
            _spriteRenderer.sprite = sprites[_currentFrame];
            _frameCounter = 0;
        }

        _frameCounter++;
    }
}