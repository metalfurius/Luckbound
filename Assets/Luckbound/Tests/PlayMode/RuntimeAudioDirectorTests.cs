using System.Collections;
using Luckbound.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class RuntimeAudioDirectorTests
    {
        private GameObject _audioOwnerForCleanup;

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

        [UnityTest, Timeout(5000)]
        public IEnumerator RepeatingTheActiveMusicTrackDoesNotRestartOrAllocateAnotherVoice()
        {
            var owner = new GameObject("RuntimeAudioDirector Same Track Idempotence Test");
            _audioOwnerForCleanup = owner;
            RuntimeAudioDirector director = owner.AddComponent<RuntimeAudioDirector>();
            director.SetVolumes(0f, 0f, 0f, 0f);
            director.MusicCrossfadeSeconds = 0f;

            Assert.That(director.MusicCatalogCount, Is.EqualTo(3));
            Assert.That(director.PlayMusic(RuntimeAudioDirector.TavernTrackId), Is.True);
            yield return new WaitUntil(() =>
                director.ActiveMusicTrackId == RuntimeAudioDirector.TavernTrackId &&
                director.ActiveMusicSourceCount == 1);

            AudioClip tavernClip = Resources.Load<AudioClip>("Luckbound/Audio/Music/tavern_theme");
            Assert.That(tavernClip, Is.Not.Null);
            Assert.That(tavernClip.samples, Is.GreaterThan(0));
            AudioSource activeSource = FindPlayingClipSource(owner, tavernClip);
            Assert.That(activeSource, Is.Not.Null);
            Assert.That(director.ActiveMusicSourceCount, Is.EqualTo(1));
            Assert.That(CountPlayingClip(owner, tavernClip), Is.EqualTo(1));
            Assert.That(director.MusicSourceCount, Is.EqualTo(2));
            Assert.That(owner.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(2));

            int midpointSamples = tavernClip.samples / 2;
            activeSource.timeSamples = midpointSamples;
            int acceptedSamples = activeSource.timeSamples;
            Assert.That(acceptedSamples, Is.EqualTo(midpointSamples).Within(Mathf.Max(1, tavernClip.frequency)));
            Assert.That(acceptedSamples, Is.GreaterThan(tavernClip.samples / 4));

            for (int request = 0; request < 2; request++)
            {
                Assert.That(director.PlayMusic(RuntimeAudioDirector.TavernTrackId), Is.True);
                Assert.That(activeSource.timeSamples, Is.GreaterThan(tavernClip.samples / 4));
                Assert.That(director.ActiveMusicTrackId, Is.EqualTo(RuntimeAudioDirector.TavernTrackId));
                Assert.That(director.ActiveMusicSourceCount, Is.EqualTo(1));
                Assert.That(CountPlayingClip(owner, tavernClip), Is.EqualTo(1));
                Assert.That(FindPlayingClipSource(owner, tavernClip), Is.SameAs(activeSource));
                Assert.That(director.MusicSourceCount, Is.EqualTo(2));
                Assert.That(director.MusicCatalogCount, Is.EqualTo(3));
                Assert.That(owner.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(2));
            }
        }

        [UnityTest, Timeout(5000)]
        public IEnumerator ReversingMusicCrossfadeReusesTheLoadedTrackVoice()
        {
            var owner = new GameObject("RuntimeAudioDirector_ReverseCrossfade");
            RuntimeAudioDirector director = owner.AddComponent<RuntimeAudioDirector>();
            director.MusicCrossfadeSeconds = 0.6f;

            Assert.That(director.PlayMusic(RuntimeAudioDirector.TavernTrackId), Is.True);
            yield return new WaitUntil(() => director.ActiveMusicTrackId == RuntimeAudioDirector.TavernTrackId);

            Assert.That(director.PlayMusic(RuntimeAudioDirector.PatioTrackId), Is.True);
            yield return new WaitUntil(() => director.ActiveMusicTrackId == RuntimeAudioDirector.PatioTrackId &&
                                             director.ActiveMusicSourceCount == 2);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(director.ActiveMusicSourceCount, Is.EqualTo(2));

            AudioClip tavernClip = Resources.Load<AudioClip>("Luckbound/Audio/Music/tavern_theme");
            Assert.That(tavernClip, Is.Not.Null);
            Assert.That(CountPlayingClip(owner, tavernClip), Is.EqualTo(1));
            Assert.That(director.PlayMusic(RuntimeAudioDirector.TavernTrackId), Is.True);

            Assert.That(director.ActiveMusicTrackId, Is.EqualTo(RuntimeAudioDirector.TavernTrackId));
            Assert.That(tavernClip.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
            Assert.That(CountPlayingClip(owner, tavernClip), Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.That(director.ActiveMusicSourceCount, Is.EqualTo(1));
            Assert.That(CountPlayingClip(owner, tavernClip), Is.EqualTo(1));

            Object.Destroy(owner);
            yield return null;
        }

        [Test]
        public void PlayEffectRejectsCallsAfterTheDirectorIsDisabled()
        {
            var owner = new GameObject("RuntimeAudioDirector_DisabledEffects");
            RuntimeAudioDirector director = owner.AddComponent<RuntimeAudioDirector>();
            director.MaximumOneShotSources = 1;

            Assert.That(director.PlayEffect("stone_step"), Is.True);
            director.enabled = false;

            Assert.That(director.PlayEffect("ui_confirm"), Is.False);
            Assert.That(director.OneShotSourceCount, Is.EqualTo(1));
            Assert.That(director.ActiveOneShotSourceCount, Is.EqualTo(0));

            Object.DestroyImmediate(owner);
        }

        [UnityTearDown]
        public IEnumerator DestroyTemporaryAudioOwner()
        {
            if (_audioOwnerForCleanup != null)
            {
                Object.Destroy(_audioOwnerForCleanup);
                _audioOwnerForCleanup = null;
                yield return null;
            }
        }

        private static AudioSource FindPlayingClipSource(GameObject owner, AudioClip clip)
        {
            AudioSource[] sources = owner.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null && sources[i].clip == clip && sources[i].isPlaying)
                {
                    return sources[i];
                }
            }

            return null;
        }

        private static int CountPlayingClip(GameObject owner, AudioClip clip)
        {
            int count = 0;
            AudioSource[] sources = owner.GetComponentsInChildren<AudioSource>();
            for (int index = 0; index < sources.Length; index++)
            {
                if (sources[index].isPlaying && sources[index].clip == clip) count++;
            }
            return count;
        }
    }
}
