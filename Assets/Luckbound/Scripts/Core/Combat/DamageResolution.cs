namespace Luckbound.Core.Combat
{
    /// <summary>
    /// The outcome of resolving one direct-damage impact.
    /// </summary>
    public readonly struct DamageResolution
    {
        internal DamageResolution(int damage, bool wasPreventedByInvulnerability)
        {
            Damage = damage;
            WasPreventedByInvulnerability = wasPreventedByInvulnerability;
        }

        public int Damage { get; }

        public bool WasPreventedByInvulnerability { get; }
    }
}
