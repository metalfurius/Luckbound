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
    }
}
