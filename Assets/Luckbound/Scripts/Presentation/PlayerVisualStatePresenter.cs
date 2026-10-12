using System;
using Luckbound.Core.Presentation;
using UnityEngine;

namespace Luckbound.Presentation
{
    /// <summary>Maps the Core visual contract to the stable Animator controller parameters.</summary>
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerVisualStatePresenter : MonoBehaviour
    {
        public const string StateParameter = "VisualState";
        public const string VelocityXParameter = "VelocityX";
        public const string VelocityYParameter = "VelocityY";
        public const string FacingXParameter = "FacingX";

        private static readonly int StateHash = Animator.StringToHash(StateParameter);
        private static readonly int VelocityXHash = Animator.StringToHash(VelocityXParameter);
        private static readonly int VelocityYHash = Animator.StringToHash(VelocityYParameter);
        private static readonly int FacingXHash = Animator.StringToHash(FacingXParameter);

        private Animator _animator;
        private SpriteRenderer _spriteRenderer;
        private IPlayerVisualStateSource _source;

        public PlayerVisualState CurrentState { get; private set; }

        public void Initialize(IPlayerVisualStateSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            _animator = GetComponent<Animator>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_animator == null)
            {
                throw new InvalidOperationException("PlayerVisualStatePresenter requires an Animator.");
            }
            if (_animator.runtimeAnimatorController == null ||
                !HasParameter(StateParameter, AnimatorControllerParameterType.Int) ||
                !HasParameter(VelocityXParameter, AnimatorControllerParameterType.Float) ||
                !HasParameter(VelocityYParameter, AnimatorControllerParameterType.Float) ||
                !HasParameter(FacingXParameter, AnimatorControllerParameterType.Float))
            {
                throw new InvalidOperationException(
                    "The player Animator must use the Luckbound visual contract: VisualState (int), VelocityX, VelocityY and FacingX (float).");
            }

            _source = source;
            ApplySnapshot(_source.CaptureVisualState());
        }

        private bool HasParameter(string parameterName, AnimatorControllerParameterType expectedType)
        {
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.name == parameterName && parameter.type == expectedType)
                {
                    return true;
                }
            }

            return false;
        }

        private void LateUpdate()
        {
            if (_source != null)
            {
                ApplySnapshot(_source.CaptureVisualState());
            }
        }

        private void ApplySnapshot(PlayerVisualSnapshot snapshot)
        {
            CurrentState = snapshot.State;
            _animator.SetInteger(StateHash, (int)snapshot.State);
            _animator.SetFloat(VelocityXHash, snapshot.VelocityX);
            _animator.SetFloat(VelocityYHash, snapshot.VelocityY);
            _animator.SetFloat(FacingXHash, snapshot.FacingX);
            if (_spriteRenderer != null && Mathf.Abs(snapshot.FacingX) > 0.001f)
            {
                _spriteRenderer.flipX = snapshot.FacingX < 0f;
            }
        }
    }
}
