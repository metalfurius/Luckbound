/*
using UnityEngine;
using System.Collections.Generic;

public class CustomWeaponAnimator : MonoBehaviour
{
    private SpriteRenderer _spriteRenderer;
    private WeaponAnimation _currentAnimation;
    private int _currentFrame;
    private Coroutine _currentAnimationCoroutine;
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

    public bool PlayAnimation(WeaponAnimation weaponAnimation)
    {
        if (!ValidateAnimation(weaponAnimation))
        {
            Debug.LogError($"Animation validation failed for {weaponAnimation?.name ?? "null"}");
            return false;
        }

        if (IsAnimating)
        {
            return false;
        }

        _currentAnimation = weaponAnimation;
        _currentFrame = 0;
        _currentAnimationCoroutine = StartCoroutine(AnimateSequence());
        return true;
    }

    private bool ValidateAnimation(WeaponAnimation weaponAnimation)
    {
        if (weaponAnimation == null) return false;
        if (weaponAnimation.sprites == null || weaponAnimation.sprites.Count == 0) return false;
        if (weaponAnimation.frameTimings == null || weaponAnimation.frameTimings.Count != weaponAnimation.sprites.Count) return false;
        if (weaponAnimation.collidersExpanded)
        {
            if (weaponAnimation.colliders == null || weaponAnimation.colliders.Count != weaponAnimation.sprites.Count) return false;
        }
        if (weaponAnimation.damagePerFrame == null || weaponAnimation.damagePerFrame.Count != weaponAnimation.sprites.Count) return false;
        return true;
    }

    private void UpdateColliderState(int frameIndex)
    {
        if (_weaponCollider == null || !_currentAnimation.collidersExpanded) return;

        var frame = _currentAnimation.colliders[frameIndex];
        _weaponCollider.offset = frame.offset;
        _weaponCollider.size = frame.size;
        _weaponCollider.enabled = frame.enabled;
    }

    private void CheckCollisionsForCurrentFrame()
    {
        if (_weaponCollider == null || !_weaponCollider.enabled) return;

        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = enemyLayer,
            useTriggers = true
        };

        var results = new List<Collider2D>();
        
        if (_weaponCollider.Overlap(filter, results) > 0)
        {
            int currentFrameDamage = (int)_currentAnimation.damagePerFrame[_currentFrame];
            
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

    private System.Collections.IEnumerator AnimateSequence()
    {
        Sprite originalSprite = _spriteRenderer.sprite;
        var waitForEndOfFrame = new WaitForEndOfFrame();

        try
        {
            while (_currentFrame < _currentAnimation.sprites.Count)
            {
                _spriteRenderer.sprite = _currentAnimation.sprites[_currentFrame];
                UpdateColliderState(_currentFrame);
                
                float frameDuration = _currentAnimation.frameTimings[_currentFrame] / animationFPS;
                float elapsedTime = 0f;
                
                while (elapsedTime < frameDuration)
                {
                    if (_currentAnimation.collidersExpanded)
                    {
                        CheckCollisionsForCurrentFrame();
                    }
                    
                    yield return waitForEndOfFrame;
                    elapsedTime += Time.deltaTime;
                }

                _currentFrame++;
                _hitEnemiesThisFrame.Clear();
            }
        }
        finally
        {
            CleanupAnimation();
            _spriteRenderer.sprite = originalSprite;
        }
    }

    private void CleanupAnimation()
    {
        if (_weaponCollider != null)
        {
            _weaponCollider.enabled = false;
        }
        _currentAnimation = null;
        _currentFrame = 0;
        _currentAnimationCoroutine = null;
        _hitEnemiesThisFrame.Clear();
    }

    private void OnDisable()
    {
        if (_currentAnimationCoroutine != null)
        {
            StopCoroutine(_currentAnimationCoroutine);
            CleanupAnimation();
        }
    }
}
*/
