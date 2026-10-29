using System;
using System.Collections.Generic;
using Luckbound.App;
using Luckbound.Core.Identifiers;
using NUnit.Framework;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ExpeditionEncounterRosterStateTests
    {
        private static readonly ActorId ActorA = ActorId.Parse("00000000000000000000000000000001");
        private static readonly ActorId ActorB = ActorId.Parse("00000000000000000000000000000002");
        private static readonly ActorId ActorC = ActorId.Parse("00000000000000000000000000000003");
        private static readonly ActorId WrongActor = ActorId.Parse("00000000000000000000000000000004");

        [Test]
        public void PlanRejectsEmptyWavesNoncontiguousNumbersAndDuplicateOrInvalidKeys()
        {
            Assert.Throws<ArgumentException>(() => new ExpeditionEncounterRosterPlan(
                Array.Empty<ExpeditionEncounterWavePlan>()));
            Assert.Throws<ArgumentException>(() => new ExpeditionEncounterWavePlan(1, Array.Empty<string>()));
            Assert.Throws<ArgumentException>(() => new ExpeditionEncounterRosterPlan(new[]
            {
                Wave(1, "spawn.one"),
                Wave(3, "spawn.three")
            }));
            Assert.Throws<ArgumentException>(() => new ExpeditionEncounterRosterPlan(new[]
            {
                Wave(1, "spawn.same"),
                Wave(2, "spawn.same")
            }));
            Assert.Throws<ArgumentException>(() => new ExpeditionEncounterWavePlan(1, new[] { " " }));
        }

        [Test]
        public void PlanCopiesWaveAndSpawnCollectionsSoCallerMutationCannotChangeTheRoster()
        {
            var sourceKeys = new List<string> { "spawn.original" };
            var sourceWaves = new List<ExpeditionEncounterWavePlan> { new ExpeditionEncounterWavePlan(1, sourceKeys) };
            var plan = new ExpeditionEncounterRosterPlan(sourceWaves);

            sourceKeys[0] = "spawn.changed";
            sourceKeys.Add("spawn.injected");
            sourceWaves.Clear();

            Assert.That(plan.Waves.Count, Is.EqualTo(1));
            Assert.That(plan.Waves[0].SpawnKeys.Count, Is.EqualTo(1));
            Assert.That(plan.Waves[0].SpawnKeys[0], Is.EqualTo("spawn.original"));
        }

        [Test]
        public void RegistrationRequiresValidUniqueActorIdsAndRejectsUnexpectedOrEarlySpawns()
        {
            var state = State(Wave(1, "spawn.one", "spawn.two"), Wave(2, "spawn.three"));

            Assert.That(state.TryRegisterSpawn("spawn.one", default), Is.False);
            Assert.That(state.Phase, Is.EqualTo(ExpeditionEncounterRosterPhase.Faulted));

            state = State(Wave(1, "spawn.one", "spawn.two"), Wave(2, "spawn.three"));
            Assert.That(state.TryRegisterSpawn("spawn.one", ActorA), Is.True);
            Assert.That(state.TryRegisterSpawn("spawn.two", ActorA), Is.False,
                "An ActorId cannot identify more than one planned spawn.");
            Assert.That(state.Phase, Is.EqualTo(ExpeditionEncounterRosterPhase.Faulted));

            state = State(Wave(1, "spawn.one", "spawn.two"));
            Assert.That(state.TryRegisterSpawn("spawn.one", ActorA), Is.True);
            Assert.That(state.TryRegisterSpawn("spawn.one", ActorB), Is.False,
                "A planned spawn can be registered only once.");
            Assert.That(state.Phase, Is.EqualTo(ExpeditionEncounterRosterPhase.Faulted));

            state = State(Wave(1, "spawn.one"), Wave(2, "spawn.two"));
            Assert.That(state.TryRegisterSpawn("spawn.two", ActorB), Is.False,
                "A future wave cannot be registered before the current wave completes.");
            Assert.That(state.Phase, Is.EqualTo(ExpeditionEncounterRosterPhase.Faulted));

            state = State(Wave(1, "spawn.one"));
            Assert.That(state.TryRegisterSpawn("spawn.unexpected", ActorA), Is.False);
            Assert.That(state.Phase, Is.EqualTo(ExpeditionEncounterRosterPhase.Faulted));
        }

        [Test]
        public void MissingRegistrationFaultsInsteadOfArmingAPartialWave()
        {
            var state = State(Wave(1, "spawn.one", "spawn.two"));
            Assert.That(state.TryRegisterSpawn("spawn.one", ActorA), Is.True);
            Assert.That(state.CanArmCurrentWave, Is.False);

            Assert.That(state.TryArmCurrentWave(), Is.False);
            Assert.That(state.Phase, Is.EqualTo(ExpeditionEncounterRosterPhase.Faulted));
            Assert.That(state.IsCleared, Is.False);
        }

        [Test]
        public void WrongActorDefeatFaultsWithoutCountingThePlannedSpawn()
        {
            var state = State(Wave(1, "spawn.one", "spawn.two"));
            RegisterAndArm(state, "spawn.one", ActorA, "spawn.two", ActorB);

            Assert.That(state.TryRecordVerifiedDefeat("spawn.one", WrongActor), Is.False);
            Assert.That(state.Phase, Is.EqualTo(ExpeditionEncounterRosterPhase.Faulted));
            Assert.That(state.DefeatedSpawnCount, Is.Zero);
            Assert.That(state.IsCleared, Is.False);
        }

        [Test]
        public void MultiWaveReadinessAndClearAdvanceSequentiallyExactlyOnce()
        {
            var state = State(Wave(1, "spawn.one", "spawn.two"), Wave(2, "spawn.three"));
            Assert.That(state.CurrentWaveNumber, Is.EqualTo(1));
            Assert.That(state.CanArmCurrentWave, Is.False);

            Assert.That(state.TryRegisterSpawn("spawn.one", ActorA), Is.True);
            Assert.That(state.CanArmCurrentWave, Is.False);
            Assert.That(state.TryRegisterSpawn("spawn.two", ActorB), Is.True);
            Assert.That(state.CanArmCurrentWave, Is.True);
            Assert.That(state.TryArmCurrentWave(), Is.True);
            Assert.That(state.Phase, Is.EqualTo(ExpeditionEncounterRosterPhase.Armed));

            Assert.That(state.TryRecordVerifiedDefeat("spawn.one", ActorA), Is.True);
            Assert.That(state.IsCleared, Is.False);
            Assert.That(state.CompletedWaveCount, Is.Zero);
            Assert.That(state.CurrentWaveNumber, Is.EqualTo(1));
            Assert.That(state.TryRecordVerifiedDefeat("spawn.two", ActorB), Is.True);
            Assert.That(state.Phase, Is.EqualTo(ExpeditionEncounterRosterPhase.Preparing));
            Assert.That(state.CompletedWaveCount, Is.EqualTo(1));
            Assert.That(state.CurrentWaveNumber, Is.EqualTo(2));
            Assert.That(state.CanArmCurrentWave, Is.False);
            Assert.That(state.TryRegisterSpawn("spawn.three", ActorC), Is.True);
            Assert.That(state.CanArmCurrentWave, Is.True);
            Assert.That(state.TryArmCurrentWave(), Is.True);
            Assert.That(state.TryRecordVerifiedDefeat("spawn.three", ActorC), Is.True);

            Assert.That(state.IsCleared, Is.True);
            Assert.That(state.Phase, Is.EqualTo(ExpeditionEncounterRosterPhase.Cleared));
            Assert.That(state.CompletedWaveCount, Is.EqualTo(2));
            Assert.That(state.CurrentWaveNumber, Is.Zero);
            Assert.That(state.RegisteredSpawnCount, Is.EqualTo(3));
            Assert.That(state.DefeatedSpawnCount, Is.EqualTo(3));
            Assert.That(state.TryRecordVerifiedDefeat("spawn.three", ActorC), Is.False);
            Assert.That(state.IsCleared, Is.True);
            Assert.That(state.CompletedWaveCount, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateDefeatDoesNotCountTwiceOrClearAnIncompleteWave()
        {
            var state = State(Wave(1, "spawn.one", "spawn.two"));
            RegisterAndArm(state, "spawn.one", ActorA, "spawn.two", ActorB);

            Assert.That(state.TryRecordVerifiedDefeat("spawn.one", ActorA), Is.True);
            Assert.That(state.TryRecordVerifiedDefeat("spawn.one", ActorA), Is.False);
            Assert.That(state.DefeatedSpawnCount, Is.EqualTo(1));
            Assert.That(state.Phase, Is.EqualTo(ExpeditionEncounterRosterPhase.Armed));
            Assert.That(state.IsCleared, Is.False);

            Assert.That(state.TryRecordVerifiedDefeat("spawn.two", ActorB), Is.True);
            Assert.That(state.IsCleared, Is.True);
            Assert.That(state.DefeatedSpawnCount, Is.EqualTo(2));
        }

        [Test]
        public void UnexpectedRemovalOfRegisteredLiveActorFaultsWithoutClear()
        {
            var state = State(Wave(1, "spawn.one"));
            Assert.That(state.TryRegisterSpawn("spawn.one", ActorA), Is.True);
            Assert.That(state.TryArmCurrentWave(), Is.True);

            Assert.That(state.TryRecordUnexpectedLiveRemoval("spawn.one", ActorA), Is.False);
            Assert.That(state.Phase, Is.EqualTo(ExpeditionEncounterRosterPhase.Faulted));
            Assert.That(state.IsCleared, Is.False);
            Assert.That(state.DefeatedSpawnCount, Is.Zero);
        }

        [Test]
        public void DisposalRejectsCapturedCallbacksAndLeavesStateDisposed()
        {
            var state = State(Wave(1, "spawn.one"));
            Assert.That(state.TryRegisterSpawn("spawn.one", ActorA), Is.True);
            Assert.That(state.TryArmCurrentWave(), Is.True);
            state.Dispose();

            Assert.That(state.TryRecordVerifiedDefeat("spawn.one", ActorA), Is.False);
            Assert.That(state.TryRegisterSpawn("spawn.one", ActorB), Is.False);
            Assert.That(state.TryRecordUnexpectedLiveRemoval("spawn.one", ActorA), Is.False);
            Assert.That(state.Phase, Is.EqualTo(ExpeditionEncounterRosterPhase.Disposed));
            Assert.That(state.IsCleared, Is.False);
        }

        private static ExpeditionEncounterWavePlan Wave(int number, params string[] keys)
        {
            return new ExpeditionEncounterWavePlan(number, keys);
        }

        private static ExpeditionEncounterRosterState State(params ExpeditionEncounterWavePlan[] waves)
        {
            return new ExpeditionEncounterRosterState(new ExpeditionEncounterRosterPlan(waves));
        }

        private static void RegisterAndArm(ExpeditionEncounterRosterState state,
            string firstKey, ActorId firstActor, string secondKey, ActorId secondActor)
        {
            Assert.That(state.TryRegisterSpawn(firstKey, firstActor), Is.True);
            Assert.That(state.TryRegisterSpawn(secondKey, secondActor), Is.True);
            Assert.That(state.TryArmCurrentWave(), Is.True);
        }
    }
}
