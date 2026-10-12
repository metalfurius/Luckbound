using System;
using System.Collections.Generic;
using UnityEngine;

namespace Luckbound.Content
{
    /// <summary>Loads and validates deterministic encounter composition from a Resources JSON asset.</summary>
    public static class CombatEncounterJsonLoader
    {
        public static CombatEncounterData Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Encounter JSON cannot be empty.", nameof(json));
            CombatEncounterData data = JsonUtility.FromJson<CombatEncounterData>(json);
            if (data == null || data.schemaVersion != 1 || string.IsNullOrWhiteSpace(data.encounterId) ||
                data.waves == null || data.waves.Length < 2 || data.waves.Length > 2)
            {
                throw new FormatException("Encounter data must use schema version 1 and define exactly two waves.");
            }

            if (data.minimumEntranceDistance < 8f || float.IsNaN(data.minimumEntranceDistance) || float.IsInfinity(data.minimumEntranceDistance))
            {
                throw new FormatException("Encounter spawns must remain at least eight world units from the entrance.");
            }

            var waveIds = new HashSet<string>(StringComparer.Ordinal);
            for (int waveIndex = 0; waveIndex < data.waves.Length; waveIndex++)
            {
                CombatEncounterWaveData wave = data.waves[waveIndex];
                if (wave == null || string.IsNullOrWhiteSpace(wave.id) || !waveIds.Add(wave.id) ||
                    wave.spawns == null || wave.spawns.Length == 0 || wave.spawns.Length > 8)
                {
                    throw new FormatException("Encounter wave " + waveIndex + " must have a unique ID and one to eight spawns.");
                }

                for (int spawnIndex = 0; spawnIndex < wave.spawns.Length; spawnIndex++)
                {
                    CombatEnemySpawnData spawn = wave.spawns[spawnIndex];
                    if (spawn == null || (spawn.enemyId != "enemy.slime" && spawn.enemyId != "enemy.huesoguardia") ||
                        float.IsNaN(spawn.x) || float.IsInfinity(spawn.x) || float.IsNaN(spawn.y) || float.IsInfinity(spawn.y))
                    {
                        throw new FormatException("Encounter spawn " + waveIndex + ":" + spawnIndex + " has an invalid enemy ID or position.");
                    }

                    float entranceDistance = Vector2.Distance(new Vector2(data.entranceX, data.entranceY), new Vector2(spawn.x, spawn.y));
                    if (entranceDistance < data.minimumEntranceDistance)
                    {
                        throw new FormatException("Encounter spawn " + waveIndex + ":" + spawnIndex + " is too close to the entrance.");
                    }
                }
            }

            return data;
        }
    }

    [Serializable]
    public sealed class CombatEncounterData
    {
        public int schemaVersion;
        public string encounterId;
        public float entranceX;
        public float entranceY;
        public float minimumEntranceDistance;
        public CombatEncounterWaveData[] waves;
    }

    [Serializable]
    public sealed class CombatEncounterWaveData
    {
        public string id;
        public CombatEnemySpawnData[] spawns;
    }

    [Serializable]
    public sealed class CombatEnemySpawnData
    {
        public string enemyId;
        public float x;
        public float y;
    }
}
