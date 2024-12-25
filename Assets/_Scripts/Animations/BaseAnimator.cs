using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public abstract class BaseAnimator : MonoBehaviour
{
    private SpriteRenderer _spriteRenderer;
    private Coroutine _currentAnimationCoroutine;
    private AnimationRequest _currentRequest;
    private readonly Queue<AnimationRequest> _animationQueue = new Queue<AnimationRequest>();
    protected bool IsAnimating => _currentAnimationCoroutine != null;

    [SerializeField] private float animationFPS = 60f;

    protected virtual void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    protected void RequestAnimation(AnimationRequest request)
    {
        if (_currentRequest == null || request.priority >= _currentRequest.priority)
        {
            StopCurrentAnimation();
            PlayAnimation(request);
        }
        else
        {
            _animationQueue.Enqueue(request);
        }
    }

    private void PlayAnimation(AnimationRequest request)
    {
        _currentRequest = request;
        _currentAnimationCoroutine = StartCoroutine(AnimateSequence(request));
    }

    private IEnumerator AnimateSequence(AnimationRequest request)
    {
        var waitForEndOfFrame = new WaitForEndOfFrame();

        try
        {
            for (int i = 0; i < request.sprites.Count; i++)
            {
                _spriteRenderer.sprite = request.sprites[i];
                request.OnFrameUpdated?.Invoke(i);

                // Update collider state if colliders are provided
                if (request.colliders != null && i < request.colliders.Count)
                {
                    UpdateColliderState(request.colliders[i]);
                }

                float frameDuration = request.frameTimings[i] / animationFPS;
                float elapsedTime = 0f;

                while (elapsedTime < frameDuration)
                {
                    yield return waitForEndOfFrame;
                    elapsedTime += Time.deltaTime;
                }
            }

            request.OnAnimationCompleted?.Invoke();
        }
        finally
        {
            CleanupAnimation();
        }
    }

    private void UpdateColliderState(BoxCollider2D boxCollider2D)
    {
        // Implement the logic to update the collider state
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
            PlayAnimation(_animationQueue.Dequeue());
        }
    }

    private void OnDisable()
    {
        StopCurrentAnimation();
    }
}