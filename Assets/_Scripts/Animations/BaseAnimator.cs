using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class AnimationRequest
{
    public List<Sprite> Sprites;
    public List<float> FrameTimings;
    public int Priority; // Higher priority interrupts lower priority animations
    public System.Action<int> OnFrameUpdated;
    public System.Action OnAnimationCompleted;
}
public abstract class BaseAnimator : MonoBehaviour
{
    protected SpriteRenderer _spriteRenderer;
    private Coroutine _currentAnimationCoroutine;
    private AnimationRequest _currentRequest;
    private readonly Queue<AnimationRequest> _animationQueue = new Queue<AnimationRequest>();
    protected bool IsAnimating => _currentAnimationCoroutine != null;

    [SerializeField] private float animationFPS = 60f;

    protected virtual void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void RequestAnimation(AnimationRequest request)
    {
        if (_currentRequest == null || request.Priority >= _currentRequest.Priority)
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
            for (int i = 0; i < request.Sprites.Count; i++)
            {
                _spriteRenderer.sprite = request.Sprites[i];
                request.OnFrameUpdated?.Invoke(i);

                float frameDuration = request.FrameTimings[i] / animationFPS;
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
