namespace Luckbound.Core.Presentation
{
    /// <summary>Engine-independent values required to present the player's resource HUD.</summary>
    public readonly struct PlayerHudSnapshot
    {
        public PlayerHudSnapshot(
            int currentHealth,
            int maximumHealth,
            int currentEnergy,
            int maximumEnergy,
            int healthPotionCharges,
            int maximumHealthPotionCharges,
            int potionPreparationTicksRemaining)
        {
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            CurrentEnergy = currentEnergy;
            MaximumEnergy = maximumEnergy;
            HealthPotionCharges = healthPotionCharges;
            MaximumHealthPotionCharges = maximumHealthPotionCharges;
            PotionPreparationTicksRemaining = potionPreparationTicksRemaining;
        }

        public int CurrentHealth { get; }

        public int MaximumHealth { get; }

        public int CurrentEnergy { get; }

        public int MaximumEnergy { get; }

        public int HealthPotionCharges { get; }

        public int MaximumHealthPotionCharges { get; }

        public int PotionPreparationTicksRemaining { get; }
    }

    public interface IPlayerHudStateSource
    {
        PlayerHudSnapshot CaptureHudState();
    }
}
