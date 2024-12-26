using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public sealed class BaseAnimator : MonoBehaviour
{
    private SpriteRenderer _spriteRenderer;
    private Coroutine _currentAnimationCoroutine;
    private AnimationRequest _currentRequest;
    private readonly Queue<AnimationRequest> _animationQueue = new Queue<AnimationRequest>();
    private readonly HashSet<Enemy> _hitEnemiesThisFrame = new HashSet<Enemy>();
    private BoxCollider2D _weaponCollider;

    [SerializeField] private float animationFPS = 60f;
    [SerializeField] private LayerMask enemyLayer;

    private bool IsAnimating => _currentAnimationCoroutine != null;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Initialize(BoxCollider2D weaponCollider)
    {
        _weaponCollider = weaponCollider;
        _weaponCollider.enabled = false;
    }

    public void PlayAnimation(AnimationRequest request)
    {
        if (_currentRequest == null || request.priority >= _currentRequest.priority)
        {
            if (_currentRequest != null && _currentRequest.animationName == request.animationName && IsAnimating)
            {
                // If the current animation is the same and it's already animating, do nothing
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
            } while (request.loop);

            request.OnAnimationCompleted?.Invoke();
        }
        finally
        {
            CleanupAnimation();
        }
    }

    private void UpdateColliderState(BoxCollider2D boxCollider2D)
    {
        if (!_weaponCollider) return;

        _weaponCollider.offset = boxCollider2D.offset;
        _weaponCollider.size = boxCollider2D.size;
        _weaponCollider.enabled = boxCollider2D.enabled;
    }

    private void CheckCollisionsForCurrentFrame(AnimationRequest request, int frameIndex)
    {
        if (!_weaponCollider || !_weaponCollider.enabled) return;

        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = enemyLayer,
            useTriggers = true
        };

        var results = new List<Collider2D>();

        if (_weaponCollider.Overlap(filter, results) > 0)
        {
            int currentFrameDamage = (int)request.damagePerFrame[frameIndex];

            foreach (var result in results)
            {
                if (result == null) continue;

                var enemy = result.GetComponent<Enemy>();
                if (enemy != null && _hitEnemiesThisFrame.Add(enemy))
                {
                    enemy.TakeDamage(currentFrameDamage);
                }
            }
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

        if (_weaponCollider)
        {
            _weaponCollider.enabled = false;
        }

        _hitEnemiesThisFrame.Clear();

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