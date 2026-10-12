using System;
using System.Collections.Generic;
using Luckbound.Core.Combat;
using UnityEngine;

namespace Luckbound.Content
{
    /// <summary>Loads and validates the Custodian encounter values from its Resources JSON definition.</summary>
    public static class CustodianBossJsonLoader
    {
        public static CustodianBossDefinition Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Custodian boss JSON cannot be empty.", nameof(json));

            CustodianBossData data;
            try
            {
                data = JsonUtility.FromJson<CustodianBossData>(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("Custodian boss JSON could not be parsed.", exception);
            }

            IReadOnlyList<string> errors = Validate(data);
            if (errors.Count > 0) throw new FormatException("Invalid Custodian boss definition: " + string.Join("; ", errors));

            return new CustodianBossDefinition(
                data.bossId,
                data.maximumHealth,
                data.defense,
                data.telegraphTicks,
                data.phaseTransitionTicks,
                data.phaseOneRecoveryTicks,
                data.phaseTwoRecoveryTicks,
                data.cutDamage,
                data.waveDamage,
                data.chargeDamage,
                data.phaseTwoWaveCount);
        }

        public static IReadOnlyList<string> Validate(CustodianBossData data)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("document is missing");
                return errors;
            }

            if (data.schemaVersion != 1) errors.Add("schemaVersion must be 1");
            if (data.bossId != "boss.custodian_of_the_oaks") errors.Add("bossId must be boss.custodian_of_the_oaks");
            if (data.maximumHealth != 900) errors.Add("maximumHealth must remain 900");
            if (data.defense != 3) errors.Add("defense must remain 3");
            if (data.telegraphTicks < 33) errors.Add("telegraphTicks must provide at least 0.55 seconds at 60 Hz");
            if (data.phaseTransitionTicks != 120) errors.Add("phaseTransitionTicks must equal 120 ticks");
            if (data.phaseOneRecoveryTicks <= 0 || data.phaseTwoRecoveryTicks <= 0 ||
                data.phaseTwoRecoveryTicks >= data.phaseOneRecoveryTicks)
                errors.Add("phase two recovery must be shorter than phase one");
            if (data.cutDamage != 14) errors.Add("cutDamage must remain 14");
            if (data.waveDamage != 12) errors.Add("waveDamage must remain 12");
            if (data.chargeDamage != 16) errors.Add("chargeDamage must remain 16");
            if (data.phaseTwoWaveCount != 2) errors.Add("phaseTwoWaveCount must equal 2");
            return errors;
        }
    }

    [Serializable]
    public sealed class CustodianBossData
    {
        public int schemaVersion;
        public string bossId;
        public int maximumHealth;
        public int defense;
        public int telegraphTicks;
        public int phaseTransitionTicks;
        public int phaseOneRecoveryTicks;
        public int phaseTwoRecoveryTicks;
        public int cutDamage;
        public int waveDamage;
        public int chargeDamage;
        public int phaseTwoWaveCount;
    }
}
