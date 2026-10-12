namespace Luckbound.Core.Presentation
{
    /// <summary>Stable visual state identifiers shared by gameplay and presentation assets.</summary>
    public enum PlayerVisualState
    {
        Idle = 0,
        Moving = 1,
        Airborne = 2,
        Attacking = 3,
        Dodging = 4,
        Stunned = 5,
        Dead = 6
    }

    /// <summary>A Unity-independent snapshot of the values needed to present one player.</summary>
    public readonly struct PlayerVisualSnapshot
    {
        public PlayerVisualSnapshot(PlayerVisualState state, float velocityX, float velocityY, float facingX)
        {
            State = state;
            VelocityX = velocityX;
            VelocityY = velocityY;
            FacingX = facingX < 0f ? -1f : 1f;
        }

        public PlayerVisualState State { get; }

        public float VelocityX { get; }

        public float VelocityY { get; }

        public float FacingX { get; }
    }

    /// <summary>Allows presentation to read player state without depending on gameplay assemblies.</summary>
    public interface IPlayerVisualStateSource
    {
        PlayerVisualSnapshot CaptureVisualState();
    }

    /// <summary>Resolves mutually exclusive animation states in the product-defined priority order.</summary>
    public static class PlayerVisualStateResolver
    {
        public static PlayerVisualState Resolve(
            bool isDead,
            bool isStunned,
            bool isDodging,
            bool isAttacking,
            bool isGrounded,
            float horizontalVelocity)
        {
            if (isDead) return PlayerVisualState.Dead;
            if (isStunned) return PlayerVisualState.Stunned;
            if (isDodging) return PlayerVisualState.Dodging;
            if (isAttacking) return PlayerVisualState.Attacking;
            if (!isGrounded) return PlayerVisualState.Airborne;
            if (horizontalVelocity < -0.05f || horizontalVelocity > 0.05f) return PlayerVisualState.Moving;
            return PlayerVisualState.Idle;
        }
    }
}
