using System.Collections;
using Luckbound.App;
using Luckbound.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ApplicationAudioRouterTests
    {
        [TestCase("Hub", RuntimeAudioDirector.TavernTrackId)]
        [TestCase("Expedition", RuntimeAudioDirector.PatioTrackId)]
        [TestCase("CombatArena", RuntimeAudioDirector.PatioTrackId)]
        [TestCase("CustodianArena", RuntimeAudioDirector.CustodianTrackId)]
        public void SceneContextResolvesTheExpectedMusic(string sceneName, string expectedTrackId)
        {
            Assert.That(ApplicationAudioRouter.TryResolveMusicTrack(sceneName, out string actualTrackId), Is.True);
            Assert.That(actualTrackId, Is.EqualTo(expectedTrackId));
        }

        [TestCase("Bootstrap")]
        [TestCase("Menu")]
        [TestCase("UnmappedScene")]
        public void UnmappedScenesRemainWithoutATheme(string sceneName)
        {
            Assert.That(ApplicationAudioRouter.TryResolveMusicTrack(sceneName, out string trackId), Is.False);
            Assert.That(trackId, Is.Null);
        }

        [TestCase("Expedition", RuntimeAmbienceDirector.ForestCourtyardId)]
        [TestCase("CombatArena", RuntimeAmbienceDirector.ForestCourtyardId)]
        public void PatioScenesResolveTheExpectedAmbience(string sceneName, string expectedAmbienceId)
        {
            Assert.That(ApplicationAudioRouter.TryResolveAmbience(sceneName, out string actualAmbienceId), Is.True);
            Assert.That(actualAmbienceId, Is.EqualTo(expectedAmbienceId));
        }

        [TestCase("Bootstrap")]
        [TestCase("Menu")]
        [TestCase("Hub")]
        [TestCase("CustodianArena")]
        [TestCase("UnmappedScene")]
        public void ScenesOutsideThePatioRemainWithoutAmbience(string sceneName)
        {
            Assert.That(ApplicationAudioRouter.TryResolveAmbience(sceneName, out string ambienceId), Is.False);
            Assert.That(ambienceId, Is.Null);
        }

        [UnityTest]
        public IEnumerator SceneMusicWaitsForInputAndUsesTheLatestContext()
        {
            var owner = new GameObject("ApplicationAudioRouter_Test");
            RuntimeAudioDirector director = owner.AddComponent<RuntimeAudioDirector>();
            ApplicationAudioRouter router = owner.AddComponent<ApplicationAudioRouter>();

            Assert.That(router.SetSceneContext("Hub"), Is.True);
            Assert.That(router.RequestedMusicTrackId, Is.EqualTo(RuntimeAudioDirector.TavernTrackId));
            Assert.That(router.UserGestureObserved, Is.False);
            Assert.That(director.ActiveMusicTrackId, Is.Null);

            router.NotifyUserGesture();
            yield return new WaitUntil(() => director.ActiveMusicTrackId == RuntimeAudioDirector.TavernTrackId);

            Assert.That(router.SetSceneContext("Expedition"), Is.True);
            yield return new WaitUntil(() => director.ActiveMusicTrackId == RuntimeAudioDirector.PatioTrackId);
            yield return new WaitUntil(() => director.ActiveMusicSourceCount == 1);
            Assert.That(director.ActiveMusicSourceCount, Is.EqualTo(1));

            Object.Destroy(owner);
            yield return null;
        }

        [UnityTest, Timeout(5000)]
        public IEnumerator PatioAmbienceWaitsForInputAndStopsOutsideThePatio()
        {
            var owner = new GameObject("ApplicationAudioRouter_AmbienceTest");
            owner.AddComponent<RuntimeAudioDirector>();
            RuntimeAmbienceDirector ambienceDirector = owner.AddComponent<RuntimeAmbienceDirector>();
            ApplicationAudioRouter router = owner.AddComponent<ApplicationAudioRouter>();

            Assert.That(router.SetSceneContext("Expedition"), Is.True);
            Assert.That(router.RequestedAmbienceId, Is.EqualTo(RuntimeAmbienceDirector.ForestCourtyardId));
            Assert.That(router.UserGestureObserved, Is.False);
            Assert.That(ambienceDirector.CurrentAmbienceId, Is.Null);

            router.NotifyUserGesture();
            yield return new WaitUntil(() => ambienceDirector.ActiveSourceCount == 1);
            Assert.That(ambienceDirector.CurrentAmbienceId, Is.EqualTo(RuntimeAmbienceDirector.ForestCourtyardId));

            Assert.That(router.SetSceneContext("CustodianArena"), Is.True);
            yield return new WaitUntil(() => ambienceDirector.ActiveSourceCount == 0);
            Assert.That(router.RequestedAmbienceId, Is.Null);
            Assert.That(ambienceDirector.CurrentAmbienceId, Is.Null);

            Object.Destroy(owner);
            yield return null;
        }
    }
}
