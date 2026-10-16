using System.Collections;
using Luckbound.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class RuntimeAudioDirectorTests
    {
        [Test]
        public void VolumeSettingsClampAndApplyMasterTimesCategory()
        {
            var owner = new GameObject("RuntimeAudioDirector_Volumes");
            RuntimeAudioDirector director = owner.AddComponent<RuntimeAudioDirector>();

            director.SetVolumes(1.4f, 0.65f, -0.5f, float.NaN);

            Assert.That(director.MasterVolume, Is.EqualTo(1f));
            Assert.That(director.MusicVolume, Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(director.EffectsVolume, Is.EqualTo(0f));
            Assert.That(director.AmbienceVolume, Is.EqualTo(0f));
            Assert.That(director.EffectiveMusicVolume, Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(director.EffectiveEffectsVolume, Is.EqualTo(0f));
            Assert.That(director.EffectiveAmbienceVolume, Is.EqualTo(0f));

            Object.DestroyImmediate(owner);
        }

        [UnityTest, Timeout(5000)]
        public IEnumerator CatalogPlaybackKeepsSceneOwnedSourcesWithinTheirCaps()
        {
            var owner = new GameObject("RuntimeAudioDirector_Playback");
            RuntimeAudioDirector director = owner.AddComponent<RuntimeAudioDirector>();
            director.MaximumOneShotSources = 2;
            director.MusicCrossfadeSeconds = 0.02f;

            Assert.That(director.EffectCatalogCount, Is.EqualTo(36));
            Assert.That(director.MusicCatalogCount, Is.EqualTo(3));
            Assert.That(director.PlayEffect("stone_step"), Is.True);
            Assert.That(director.PlayEffect("ui_confirm"), Is.True);
            director.PlayEffect("boss_appear");
            Assert.That(director.OneShotSourceCount, Is.LessThanOrEqualTo(2));

            Assert.That(director.PlayMusic(RuntimeAudioDirector.TavernTrackId), Is.True);
            yield return new WaitUntil(() => director.ActiveMusicTrackId == RuntimeAudioDirector.TavernTrackId);
            Assert.That(director.PlayMusic(RuntimeAudioDirector.PatioTrackId), Is.True);
            yield return new WaitUntil(() => director.ActiveMusicTrackId == RuntimeAudioDirector.PatioTrackId);
            Assert.That(director.MusicSourceCount, Is.LessThanOrEqualTo(2));
            yield return new WaitForSecondsRealtime(0.05f);
            Assert.That(director.ActiveMusicSourceCount, Is.LessThanOrEqualTo(1));

            Assert.That(director.PlayMusic(RuntimeAudioDirector.CustodianTrackId), Is.True);
            yield return new WaitUntil(() => director.ActiveMusicTrackId == RuntimeAudioDirector.CustodianTrackId);
            Assert.That(director.MusicSourceCount, Is.LessThanOrEqualTo(2));
            director.StopMusic();
            yield return new WaitForSecondsRealtime(0.05f);
            Assert.That(director.ActiveMusicSourceCount, Is.EqualTo(0));

            Assert.That(director.PlayMusic(RuntimeAudioDirector.TavernTrackId), Is.True);
            director.enabled = false;
            Assert.That(director.ActiveMusicSourceCount, Is.EqualTo(0));
            Assert.That(director.ActiveOneShotSourceCount, Is.EqualTo(0));

            Object.Destroy(owner);
            yield return null;
        }
    }
}
