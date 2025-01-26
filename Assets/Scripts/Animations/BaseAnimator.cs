using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public sealed class BaseAnimator : MonoBehaviour
{
    private SpriteRenderer _spriteRenderer;
    private Coroutine _currentAnimationCoroutine;
    private AnimationRequest _currentRequest;
    private readonly Queue<AnimationRequest> _animationQueue = new Queue<AnimationRequest>();

    [SerializeField] private float animationFPS = 60f;

    private bool IsAnimating => _currentAnimationCoroutine != null;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void PlayAnimation(AnimationRequest request)
    {
        if (_currentRequest == null || request.priority >= _currentRequest.priority)
        {
            if (_currentRequest != null && _currentRequest.animationName == request.animationName && IsAnimating)
            {
                return;
            }

            StopCurrentAnimation();
            StartAnimation(request);
        }
        else
        {
            _animationQueue.Enqueue(request);
        }
    }

    private void StartAnimation(AnimationRequest request)
    {
        _currentRequest = request;
        _currentAnimationCoroutine = StartCoroutine(AnimateSequence(request));
    }

    private IEnumerator AnimateSequence(AnimationRequest request)
    {
        var waitForEndOfFrame = new WaitForEndOfFrame();

        try
        {
            do
            {
                for (int i = 0; i < request.sprites.Count; i++)
                {
                    _spriteRenderer.sprite = request.sprites[i];
                    request.OnFrameUpdated?.Invoke(i);
                    
                    float frameDuration = request.frameTimings[i] / animationFPS;
                    float elapsedTime = 0f;

                    while (elapsedTime < frameDuration)
                    {
                        yield return waitForEndOfFrame;
                        elapsedTime += Time.deltaTime;
                    }
                }
            } while (request.loop);

            request.OnAnimationCompleted?.Invoke();
        }
        finally
        {
            CleanupAnimation();
        }
    }
    private void StopCurrentAnimation()
    {
        if (_currentAnimationCoroutine != null)
        {
            StopCoroutine(_currentAnimationCoroutine);
        }
        CleanupAnimation();
    }

    private void CleanupAnimation()
    {
        _currentRequest = null;
        _currentAnimationCoroutine = null;
        if (_animationQueue.Count > 0)
        {
            StartAnimation(_animationQueue.Dequeue());
        }
    }

    private void OnDisable()
    {
        StopCurrentAnimation();
    }
}