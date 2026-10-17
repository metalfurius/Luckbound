using System;
using System.Collections.Generic;
using Luckbound.App;
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
                ApplicationAudioRouter.BossAppearEffectId,
                ApplicationAudioRouter.BossDeathEffectId,
                ApplicationAudioRouter.SwordSwingEffectId,
                ApplicationAudioRouter.SwordImpactEffectId
            };

            foreach (string effectId in requiredIds)
                Assert.That(effectIds.Contains(effectId), Is.True, "Missing audio effect ID: " + effectId);
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
    }
}
