using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Core.Identifiers;

namespace Luckbound.App
{
    /// <summary>
    /// Immutable description of one encounter wave. The keys must come from the validated content provider;
    /// this type preserves and compares them but does not invent a canonical content-key encoding.
    /// </summary>
    internal sealed class ExpeditionEncounterWavePlan
    {
        private readonly IReadOnlyList<string> _spawnKeys;

        internal ExpeditionEncounterWavePlan(int waveNumber, IEnumerable<string> stableSpawnKeys)
        {
            if (waveNumber < 1) throw new ArgumentOutOfRangeException(nameof(waveNumber));
            if (stableSpawnKeys == null) throw new ArgumentNullException(nameof(stableSpawnKeys));

            string[] keys = stableSpawnKeys.ToArray();
            if (keys.Length == 0)
                throw new ArgumentException("Encounter waves must contain at least one planned spawn.", nameof(stableSpawnKeys));

            for (int i = 0; i < keys.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(keys[i]) || !string.Equals(keys[i], keys[i].Trim(), StringComparison.Ordinal))
                    throw new ArgumentException("Spawn keys must be non-empty stable content keys without surrounding whitespace.", nameof(stableSpawnKeys));
            }

            WaveNumber = waveNumber;
            _spawnKeys = Array.AsReadOnly(keys);
        }

        internal int WaveNumber { get; }
        internal IReadOnlyList<string> SpawnKeys => _spawnKeys;
    }

    /// <summary>
    /// Complete immutable encounter roster. This validates structural completeness and exact key uniqueness;
    /// durable meaning/stability of each key remains the validated content provider's responsibility.
    /// </summary>
    internal sealed class ExpeditionEncounterRosterPlan
    {
        private readonly IReadOnlyList<ExpeditionEncounterWavePlan> _waves;

        internal ExpeditionEncounterRosterPlan(IEnumerable<ExpeditionEncounterWavePlan> waves)
        {
            if (waves == null) throw new ArgumentNullException(nameof(waves));

            ExpeditionEncounterWavePlan[] copiedWaves = waves.ToArray();
            if (copiedWaves.Length == 0)
                throw new ArgumentException("An encounter roster must contain at least one wave.", nameof(waves));

            var seenSpawnKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < copiedWaves.Length; i++)
            {
                ExpeditionEncounterWavePlan wave = copiedWaves[i];
                if (wave == null)
                    throw new ArgumentException("Encounter roster waves cannot be null.", nameof(waves));
                if (wave.WaveNumber != i + 1)
                    throw new ArgumentException("Encounter wave numbers must be contiguous and ordered from one.", nameof(waves));

                foreach (string spawnKey in wave.SpawnKeys)
                {
                    if (!seenSpawnKeys.Add(spawnKey))
                        throw new ArgumentException("Spawn keys must be unique across the complete encounter roster.", nameof(waves));
                }
            }

            _waves = Array.AsReadOnly(copiedWaves);
        }

        internal IReadOnlyList<ExpeditionEncounterWavePlan> Waves => _waves;
    }

    internal enum ExpeditionEncounterRosterPhase
    {
        Preparing,
        Armed,
        Cleared,
        Faulted,
        Disposed
    }

    /// <summary>
    /// Unity-independent roster/phase aggregator for facts supplied by a future trusted App/Gameplay adapter.
    /// This is not proof of Unity physical death, victory authority, settlement, a completion ticket, or a public
    /// production API. In particular, TryRecordVerifiedDefeat can only validate the supplied key/ActorId pairing;
    /// it cannot establish that a real actor died through combat.
    /// </summary>
    internal sealed class ExpeditionEncounterRosterState : IDisposable
    {
        private sealed class SpawnState
        {
            internal SpawnState(int waveIndex)
            {
                WaveIndex = waveIndex;
            }

            internal int WaveIndex { get; }
            internal bool IsRegistered { get; set; }
            internal ActorId ActorId { get; set; }
            internal bool IsDefeated { get; set; }
        }

        private readonly ExpeditionEncounterRosterPlan _plan;
        private readonly Dictionary<string, SpawnState> _spawnsByKey;
        private readonly HashSet<ActorId> _registeredActorIds = new HashSet<ActorId>();
        private int _currentWaveIndex;
        private int _registeredSpawnCount;
        private int _defeatedSpawnCount;
        private int _completedWaveCount;
        private ExpeditionEncounterRosterPhase _phase;

        internal ExpeditionEncounterRosterState(ExpeditionEncounterRosterPlan plan)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _spawnsByKey = new Dictionary<string, SpawnState>(StringComparer.Ordinal);

            for (int waveIndex = 0; waveIndex < plan.Waves.Count; waveIndex++)
            {
                foreach (string spawnKey in plan.Waves[waveIndex].SpawnKeys)
                    _spawnsByKey.Add(spawnKey, new SpawnState(waveIndex));
            }

            _phase = ExpeditionEncounterRosterPhase.Preparing;
        }

        internal ExpeditionEncounterRosterPhase Phase => _phase;
        internal int WaveCount => _plan.Waves.Count;
        internal int CurrentWaveNumber => _currentWaveIndex < _plan.Waves.Count ? _currentWaveIndex + 1 : 0;
        internal int CompletedWaveCount => _completedWaveCount;
        internal int RegisteredSpawnCount => _registeredSpawnCount;
        internal int DefeatedSpawnCount => _defeatedSpawnCount;
        internal bool IsCleared => _phase == ExpeditionEncounterRosterPhase.Cleared;

        /// <summary>True only when every planned spawn in the next sequential wave has one valid registration.</summary>
        internal bool CanArmCurrentWave
        {
            get
            {
                if (_phase != ExpeditionEncounterRosterPhase.Preparing || _currentWaveIndex >= _plan.Waves.Count)
                    return false;

                foreach (string spawnKey in _plan.Waves[_currentWaveIndex].SpawnKeys)
                {
                    if (!_spawnsByKey[spawnKey].IsRegistered) return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Registers one initialized actor for a planned spawn in the current wave. Registration of a future
        /// wave is rejected so its actors cannot enter the active roster before that wave is ready.
        /// </summary>
        internal bool TryRegisterSpawn(string spawnKey, ActorId actorId)
        {
            if (IsTerminal()) return false;
            if (_phase != ExpeditionEncounterRosterPhase.Preparing || _currentWaveIndex >= _plan.Waves.Count)
                return FailClosed();
            if (!actorId.IsValid || string.IsNullOrEmpty(spawnKey) ||
                !_spawnsByKey.TryGetValue(spawnKey, out SpawnState spawn) ||
                spawn.WaveIndex != _currentWaveIndex || spawn.IsRegistered ||
                _registeredActorIds.Contains(actorId))
                return FailClosed();

            spawn.ActorId = actorId;
            spawn.IsRegistered = true;
            _registeredActorIds.Add(actorId);
            _registeredSpawnCount++;
            return true;
        }

        /// <summary>
        /// Arms only the current sequential wave, and only after every expected spawn in that wave is registered.
        /// An attempted arm with an incomplete roster faults the aggregator rather than silently shrinking it.
        /// </summary>
        internal bool TryArmCurrentWave()
        {
            if (IsTerminal()) return false;
            if (!CanArmCurrentWave) return FailClosed();

            _phase = ExpeditionEncounterRosterPhase.Armed;
            return true;
        }

        /// <summary>
        /// Accepts one defeat fact only for the exact registered spawn/ActorId pair. The caller must be a trusted
        /// adapter that independently verified the real actor's combat-death state; this method does not do so.
        /// Repeated matching defeat callbacks are rejected without counting twice.
        /// </summary>
        internal bool TryRecordVerifiedDefeat(string spawnKey, ActorId verifiedActorId)
        {
            if (IsTerminal()) return false;

            if (string.IsNullOrEmpty(spawnKey) || !_spawnsByKey.TryGetValue(spawnKey, out SpawnState spawn) ||
                !spawn.IsRegistered || !verifiedActorId.IsValid || spawn.ActorId != verifiedActorId)
                return FailClosed();

            if (spawn.IsDefeated) return false;
            if (_phase != ExpeditionEncounterRosterPhase.Armed || spawn.WaveIndex != _currentWaveIndex)
                return FailClosed();

            spawn.IsDefeated = true;
            _defeatedSpawnCount++;
            if (IsCurrentWaveDefeated()) CompleteCurrentWave();
            return true;
        }

        /// <summary>
        /// Receives an adapter's report that a registered actor disappeared while still live. Such a report
        /// faults the roster; object absence is never translated into a defeat or clear fact.
        /// </summary>
        internal bool TryRecordUnexpectedLiveRemoval(string spawnKey, ActorId actorId)
        {
            if (IsTerminal()) return false;

            if (string.IsNullOrEmpty(spawnKey) || !_spawnsByKey.TryGetValue(spawnKey, out SpawnState spawn) ||
                !spawn.IsRegistered || !actorId.IsValid || spawn.ActorId != actorId)
                return FailClosed();

            return FailClosed();
        }

        public void Dispose()
        {
            if (_phase == ExpeditionEncounterRosterPhase.Disposed) return;

            // Invalidate the aggregator before clearing its mutable registration tables. Any captured callback
            // that runs after this point is rejected without reviving or changing the disposed state.
            _phase = ExpeditionEncounterRosterPhase.Disposed;
            _registeredActorIds.Clear();
            _spawnsByKey.Clear();
        }

        private bool IsCurrentWaveDefeated()
        {
            foreach (string spawnKey in _plan.Waves[_currentWaveIndex].SpawnKeys)
            {
                if (!_spawnsByKey[spawnKey].IsDefeated) return false;
            }

            return true;
        }

        private void CompleteCurrentWave()
        {
            _completedWaveCount++;
            _currentWaveIndex++;
            _phase = _currentWaveIndex == _plan.Waves.Count
                ? ExpeditionEncounterRosterPhase.Cleared
                : ExpeditionEncounterRosterPhase.Preparing;
        }

        private bool IsTerminal()
        {
            return _phase == ExpeditionEncounterRosterPhase.Cleared ||
                _phase == ExpeditionEncounterRosterPhase.Faulted ||
                _phase == ExpeditionEncounterRosterPhase.Disposed;
        }

        private bool FailClosed()
        {
            if (_phase != ExpeditionEncounterRosterPhase.Cleared &&
                _phase != ExpeditionEncounterRosterPhase.Disposed)
                _phase = ExpeditionEncounterRosterPhase.Faulted;
            return false;
        }
    }
}
