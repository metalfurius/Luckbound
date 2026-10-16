using System.Collections;
using Luckbound.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class RuntimeAmbienceDirectorTests
    {
        private GameObject _owner;

        [UnityTearDown]
        public IEnumerator CleanupOwner()
        {
            if (_owner != null) Object.Destroy(_owner);
            yield return null;
        }

        [Test]
        public void ThreeLoopResourcesAreAvailableAndHaveTheExpectedDuration()
        {
            AssertLoopResource(RuntimeAmbienceDirector.ForestCourtyardId);
            AssertLoopResource(RuntimeAmbienceDirector.ArchiveId);
            AssertLoopResource(RuntimeAmbienceDirector.ForgeId);
        }

        [UnityTest, Timeout(5000)]
        public IEnumerator CurrentRequestIsIdempotentAndAmbienceVolumeIsIndependent()
        {
            RuntimeAmbienceDirector director = CreateDirector();
            director.CrossfadeSeconds = 0f;
            director.AmbienceVolume = 0.24f;

            Assert.That(director.PlayAmbience(RuntimeAmbienceDirector.ForestCourtyardId), Is.True);
            yield return new WaitUntil(() => director.ActiveSourceCount == 1);

            AudioSource source = _owner.GetComponentInChildren<AudioSource>();
            AudioClip clip = source.clip;
            Assert.That(source.loop, Is.True);
            director.SetAmbienceVolume(0.37f);
            Assert.That(director.AmbienceVolume, Is.EqualTo(0.37f).Within(0.0001f));
            Assert.That(source.volume, Is.EqualTo(0.37f).Within(0.0001f));
            Assert.That(director.PlayAmbience(RuntimeAmbienceDirector.ForestCourtyardId), Is.True);
            yield return null;

            Assert.That(director.ActiveSourceCount, Is.EqualTo(1));
            Assert.That(source.isPlaying, Is.True);
            Assert.That(source.clip, Is.SameAs(clip));
            Assert.That(director.CurrentAmbienceId, Is.EqualTo(RuntimeAmbienceDirector.ForestCourtyardId));
            Assert.That(director.PlayAmbience("unknown"), Is.False);
        }

        [UnityTest, Timeout(5000)]
        public IEnumerator ContextChangesCrossfadeWithinTwoSourcesAndCanReverse()
        {
            RuntimeAmbienceDirector director = CreateDirector();
            director.CrossfadeSeconds = 0.16f;

            Assert.That(director.PlayAmbience(RuntimeAmbienceDirector.ArchiveId), Is.True);
            yield return new WaitUntil(() => director.ActiveSourceCount == 1);
            Assert.That(director.PlayAmbience(RuntimeAmbienceDirector.ForgeId), Is.True);
            yield return new WaitUntil(() => director.ActiveSourceCount == 2);
            Assert.That(director.SourceCount, Is.EqualTo(2));
            Assert.That(director.ActiveSourceCount, Is.LessThanOrEqualTo(2));
            AudioSource[] transitioningSources = _owner.GetComponentsInChildren<AudioSource>();
            for (int index = 0; index < transitioningSources.Length; index++)
            {
                if (transitioningSources[index].isPlaying)
                    Assert.That(transitioningSources[index].loop, Is.True);
            }

            Assert.That(director.PlayAmbience(RuntimeAmbienceDirector.ArchiveId), Is.True);
            yield return new WaitUntil(() => director.CurrentAmbienceId == RuntimeAmbienceDirector.ArchiveId &&
                                             director.ActiveSourceCount == 1);
            Assert.That(director.ActiveSourceCount, Is.EqualTo(1));
            Assert.That(CountPlayingClip(_owner, Resources.Load<AudioClip>(
                "Luckbound/Audio/Ambience/archive")), Is.EqualTo(1));
            Assert.That(CountPlayingClip(_owner, Resources.Load<AudioClip>(
                "Luckbound/Audio/Ambience/forge")), Is.EqualTo(0));
        }

        [UnityTest, Timeout(5000)]
        public IEnumerator ExplicitStopFadesOutAndReleasesEverySceneOwnedVoice()
        {
            RuntimeAmbienceDirector director = CreateDirector();
            director.CrossfadeSeconds = 0.04f;
            Assert.That(director.PlayAmbience(RuntimeAmbienceDirector.ForestCourtyardId), Is.True);
            yield return new WaitUntil(() => director.ActiveSourceCount == 1);

            director.StopAmbience();
            Assert.That(director.CurrentAmbienceId, Is.Null);
            yield return new WaitUntil(() => director.ActiveSourceCount == 0);
            Assert.That(director.SourceCount, Is.EqualTo(2));
            Assert.That(director.ActiveSourceCount, Is.EqualTo(0));

            director.StopAmbience();
            Assert.That(director.ActiveSourceCount, Is.EqualTo(0));
        }

        [Test]
        public void VolumeAndCrossfadeInputsAreClampedToTheirContracts()
        {
            RuntimeAmbienceDirector director = CreateDirector();

            director.AmbienceVolume = 1.4f;
            director.CrossfadeSeconds = 9f;
            Assert.That(director.AmbienceVolume, Is.EqualTo(1f));
            Assert.That(director.CrossfadeSeconds, Is.EqualTo(2f));

            director.AmbienceVolume = float.NaN;
            director.CrossfadeSeconds = float.NaN;
            Assert.That(director.AmbienceVolume, Is.EqualTo(0f));
            Assert.That(director.CrossfadeSeconds, Is.EqualTo(0f));
        }

        private RuntimeAmbienceDirector CreateDirector()
        {
            _owner = new GameObject("RuntimeAmbienceDirector_TestOwner");
            return _owner.AddComponent<RuntimeAmbienceDirector>();
        }

        private static void AssertLoopResource(string ambienceId)
        {
            AudioClip clip = Resources.Load<AudioClip>("Luckbound/Audio/Ambience/" + ambienceId);
            Assert.That(clip, Is.Not.Null, ambienceId + " must be imported under Resources.");
            Assert.That(clip.length, Is.EqualTo(16f).Within(0.02f));
            Assert.That(clip.channels, Is.EqualTo(2));
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
