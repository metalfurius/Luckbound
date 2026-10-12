using UnityEngine;

namespace Luckbound.App
{
    /// <summary>Forwards real player contact with the end portal to its scene composition root.</summary>
    public sealed class MovementDemoGoalTrigger : MonoBehaviour
    {
        [SerializeField] private MovementDemoComposition _composition;

        public MovementDemoComposition Composition
        {
            get => _composition;
            set => _composition = value;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_composition != null) _composition.EnterGoal(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (_composition != null) _composition.EnterGoal(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (_composition != null) _composition.ExitGoal(other);
        }
    }
}
