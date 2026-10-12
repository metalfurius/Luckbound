using System;

namespace Luckbound.Core.Combat
{
    /// <summary>Immutable data contract for the provisional Custodian encounter.</summary>
    public sealed class CustodianBossDefinition
    {
        public CustodianBossDefinition(
            string bossId,
            int maximumHealth,
            int defense,
            int telegraphTicks,
            int phaseTransitionTicks,
            int phaseOneRecoveryTicks,
            int phaseTwoRecoveryTicks,
            int cutDamage,
            int waveDamage,
            int chargeDamage,
            int phaseTwoWaveCount)
        {
            if (string.IsNullOrWhiteSpace(bossId)) throw new ArgumentException("A boss ID is required.", nameof(bossId));
            if (maximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumHealth));
            if (defense < 0) throw new ArgumentOutOfRangeException(nameof(defense));
            if (telegraphTicks < 33) throw new ArgumentOutOfRangeException(nameof(telegraphTicks), "Boss warnings must last at least 0.55 seconds at 60 Hz.");
            if (phaseTransitionTicks != 120) throw new ArgumentOutOfRangeException(nameof(phaseTransitionTicks), "The phase transition must last two seconds at 60 Hz.");
            if (phaseOneRecoveryTicks <= 0 || phaseTwoRecoveryTicks <= 0 || phaseTwoRecoveryTicks >= phaseOneRecoveryTicks)
                throw new ArgumentOutOfRangeException(nameof(phaseTwoRecoveryTicks), "Phase two must recover sooner than phase one.");
            if (cutDamage <= 0 || waveDamage <= 0 || chargeDamage <= 0) throw new ArgumentOutOfRangeException(nameof(cutDamage));
            if (phaseTwoWaveCount != 2) throw new ArgumentOutOfRangeException(nameof(phaseTwoWaveCount), "The second phase must create two waves per wave pattern.");

            BossId = bossId;
            MaximumHealth = maximumHealth;
            Defense = defense;
            TelegraphTicks = telegraphTicks;
            PhaseTransitionTicks = phaseTransitionTicks;
            PhaseOneRecoveryTicks = phaseOneRecoveryTicks;
            PhaseTwoRecoveryTicks = phaseTwoRecoveryTicks;
            CutDamage = cutDamage;
            WaveDamage = waveDamage;
            ChargeDamage = chargeDamage;
            PhaseTwoWaveCount = phaseTwoWaveCount;
        }

        public string BossId { get; }
        public int MaximumHealth { get; }
        public int Defense { get; }
        public int TelegraphTicks { get; }
        public int PhaseTransitionTicks { get; }
        public int PhaseOneRecoveryTicks { get; }
        public int PhaseTwoRecoveryTicks { get; }
        public int CutDamage { get; }
        public int WaveDamage { get; }
        public int ChargeDamage { get; }
        public int PhaseTwoWaveCount { get; }
    }
}
