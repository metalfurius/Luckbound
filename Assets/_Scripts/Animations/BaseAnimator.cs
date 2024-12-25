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

    public void PlayAnimation(AnimationRequest request)
    {
        if (_currentRequest == null || request.priority >= _currentRequest.priority)
        {
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
                    if (request.colliders != null)
                    {
                        CheckCollisionsForCurrentFrame(request, i);
                    }

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

    private void CheckCollisionsForCurrentFrame(AnimationRequest request, int frameIndex)
    {
        // Implement the logic to check collisions and apply damage
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