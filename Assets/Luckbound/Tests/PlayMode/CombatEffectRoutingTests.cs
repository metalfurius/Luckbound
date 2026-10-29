using System;
using System.Collections.Generic;
using System.Reflection;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.PlayMode
{
    public sealed class CombatEffectRoutingTests
    {
        private const string EffectCatalogPath = "Luckbound/Data/AudioEffects";

        [Test]
        public void CombatEffectIdsResolveInTheShippedCatalog()
        {
            TextAsset catalogAsset = Resources.Load<TextAsset>(EffectCatalogPath);
            Assert.That(catalogAsset, Is.Not.Null);
            EffectCatalog catalog = JsonUtility.FromJson<EffectCatalog>(catalogAsset.text);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.effects, Is.Not.Null);

            var effectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (EffectEntry effect in catalog.effects)
                if (effect != null) effectIds.Add(effect.id);

            string[] requiredIds =
            {
                ApplicationAudioRouter.EnemyDeathEffectId,
                ApplicationAudioRouter.EnemyDamageEffectId,
                ApplicationAudioRouter.LandingEffectId,
                ApplicationAudioRouter.BossAppearEffectId,
                ApplicationAudioRouter.BossDeathEffectId,
                ApplicationAudioRouter.SwordSwingEffectId,
                ApplicationAudioRouter.SwordImpactEffectId,
                ApplicationAudioRouter.JumpEffectId,
                ApplicationAudioRouter.DodgeEffectId,
                ApplicationAudioRouter.PlayerDamageEffectId,
                ApplicationAudioRouter.PotionEffectId,
                ApplicationAudioRouter.BurnEffectId
            };

            foreach (string effectId in requiredIds)
                Assert.That(effectIds.Contains(effectId), Is.True, "Missing audio effect ID: " + effectId);
        }

        [Test]
        public void CombatCountersConsumeOnlyNewEventsAndResetWithoutReplaying()
        {
            int reportedCount = 3;

            Assert.That(ConsumeCounterDelta(ref reportedCount, 3), Is.EqualTo(0), "The captured baseline is not replayed.");
            Assert.That(ConsumeCounterDelta(ref reportedCount, 5), Is.EqualTo(2));
            Assert.That(ConsumeCounterDelta(ref reportedCount, 5), Is.EqualTo(0), "An observed count is not replayed.");
            Assert.That(ConsumeCounterDelta(ref reportedCount, 1), Is.EqualTo(0), "A reset establishes a new baseline.");
            Assert.That(reportedCount, Is.EqualTo(1));
            Assert.That(ConsumeCounterDelta(ref reportedCount, 2), Is.EqualTo(1));
        }

        [Test]
        public void EnemyDamageAudioBaselineStartsFreshForEachWave()
        {
            var owner = new GameObject("CombatEffectRouting_WaveBaselineTest");
            try
            {
                CombatArenaComposition composition = owner.AddComponent<CombatArenaComposition>();
                FieldInfo reportedCount = typeof(CombatArenaComposition).GetField(
                    "_reportedSuccessfulPlayerHits",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo spawnWave = typeof(CombatArenaComposition).GetMethod(
                    "SpawnWave",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(reportedCount, Is.Not.Null);
                Assert.That(spawnWave, Is.Not.Null);

                reportedCount.SetValue(composition, 3);
                var newWave = new CombatEncounterWaveData
                {
                    id = "wave.audio-baseline-test",
                    spawns = Array.Empty<CombatEnemySpawnData>()
                };
                spawnWave.Invoke(composition, new object[] { newWave, -1 });

                int baseline = (int)reportedCount.GetValue(composition);
                Assert.That(baseline, Is.Zero, "A newly spawned wave starts with no player hits.");
                Assert.That(ConsumeCounterDelta(ref baseline, 4), Is.EqualTo(4),
                    "New-wave hits must not be reduced by the previous wave's count.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PreGestureEffectsWaitForInputAndExpireAtTheSceneBoundary()
        {
            var owner = new GameObject("CombatEffectRouting_NoPlaybackTest");
            RuntimeAudioDirector director = owner.AddComponent<RuntimeAudioDirector>();
            ApplicationAudioRouter router = owner.AddComponent<ApplicationAudioRouter>();

            router.SetSceneContext("CombatArena");
            Assert.That(router.PlayEffect(ApplicationAudioRouter.EnemyDeathEffectId), Is.True);
            Assert.That(router.PendingEffectRequestCount, Is.EqualTo(1));
            Assert.That(director.OneShotSourceCount, Is.EqualTo(0));
            Assert.That(director.ActiveOneShotSourceCount, Is.EqualTo(0));

            router.SetSceneContext("CombatArena");
            Assert.That(router.PendingEffectRequestCount, Is.EqualTo(1));

            router.SetSceneContext("CustodianArena");
            Assert.That(router.PendingEffectRequestCount, Is.EqualTo(0));
            Assert.That(director.OneShotSourceCount, Is.EqualTo(0));
            Assert.That(director.ActiveOneShotSourceCount, Is.EqualTo(0));

            UnityEngine.Object.DestroyImmediate(owner);
        }

        [Serializable]
        private sealed class EffectCatalog
        {
            public EffectEntry[] effects;
        }

        [Serializable]
        private sealed class EffectEntry
        {
            public string id;
        }

        private static int ConsumeCounterDelta(ref int reportedCount, int currentCount)
        {
            MethodInfo method = typeof(CombatArenaComposition).GetMethod(
                "ConsumeCounterDelta",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            object[] arguments = { reportedCount, currentCount };
            int newEvents = (int)method.Invoke(null, arguments);
            reportedCount = (int)arguments[0];
            return newEvents;
        }
    }
}
