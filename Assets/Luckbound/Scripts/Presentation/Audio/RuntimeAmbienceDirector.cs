using System;
using System.Collections;
using UnityEngine;

namespace Luckbound.Presentation.Audio
{
    /// <summary>
    /// Scene-owned playback for the three imported ambience loops. The director
    /// creates at most two local voices and never persists or creates a singleton.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeAmbienceDirector : MonoBehaviour
    {
        public const string ForestCourtyardId = "forest_courtyard";
        public const string ArchiveId = "archive";
        public const string ForgeId = "forge";

        private const string ResourcePrefix = "Luckbound/Audio/Ambience/";
        private const float DefaultCrossfadeSeconds = 0.8f;
        private const float MaximumCrossfadeSeconds = 2f;

        [SerializeField, Range(0f, 1f)] private float _ambienceVolume = 0.45f;
        [SerializeField, Range(0f, MaximumCrossfadeSeconds)] private float _crossfadeSeconds = DefaultCrossfadeSeconds;

        private AudioSource[] _sources;
        private string[] _sourceIds;
        private float[] _envelopes;
        private Coroutine _transition;
        private Coroutine _pendingLoad;
        private AudioClip _pendingClip;
        private string _pendingId;
        private string _requestedId;
        private int _activeSourceIndex = -1;

        /// <summary>The ambience currently playing or targeted by an active crossfade.</summary>
        public string CurrentAmbienceId => _requestedId;

        public float AmbienceVolume
        {
            get => _ambienceVolume;
            set
            {
                _ambienceVolume = ClampVolume(value);
                ApplySourceVolumes();
            }
        }

        public float CrossfadeSeconds
        {
            get => _crossfadeSeconds;
            set => _crossfadeSeconds = ClampCrossfade(value);
        }

        public int SourceCount => _sources == null ? 0 : _sources.Length;

        public int ActiveSourceCount
        {
            get
            {
                if (_sources == null) return 0;
                int count = 0;
                for (int index = 0; index < _sources.Length; index++)
                {
                    if (_sources[index] != null && _sources[index].isPlaying) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// Starts or changes to a known ambience loop. Repeating the current request
        /// leaves its source and transition untouched.
        /// </summary>
        public bool PlayAmbience(string ambienceId)
        {
            if (!isActiveAndEnabled || !TryGetResourcePath(ambienceId, out string resourcePath))
            {
                return false;
            }

            if (string.Equals(_pendingId, ambienceId, StringComparison.Ordinal) && _pendingLoad != null)
            {
                return true;
            }
            if (string.Equals(_requestedId, ambienceId, StringComparison.Ordinal) && HasPlayingAmbience(ambienceId))
            {
                return true;
            }

            if (TryFindPlayingSource(ambienceId, out int existingIndex))
            {
                CancelPendingLoad();
                TransitionToExistingSource(ambienceId, existingIndex);
                return true;
            }

            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null || clip.loadState == AudioDataLoadState.Failed)
            {
                return false;
            }

            if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData() &&
                clip.loadState != AudioDataLoadState.Loaded && clip.loadState != AudioDataLoadState.Loading)
            {
                return false;
            }
            if (clip.loadState == AudioDataLoadState.Failed) return false;

            CancelPendingLoad();
            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                _pendingId = ambienceId;
                _pendingClip = clip;
                _pendingLoad = StartCoroutine(WaitForClipThenPlay(ambienceId, clip));
                return true;
            }

            BeginTransitionToNewSource(ambienceId, clip);
            return true;
        }

        /// <summary>Explicitly fades out and releases all ambience voices owned by this scene.</summary>
        public void StopAmbience()
        {
            CancelPendingLoad();
            _requestedId = null;
            if (_sources == null) return;

            CancelTransition(collapsePlayingVoices: false);
            bool anyPlaying = false;
            for (int index = 0; index < _sources.Length; index++)
            {
                anyPlaying |= IsSourcePlaying(index);
            }

            if (!anyPlaying || _crossfadeSeconds <= 0f)
            {
                StopAllVoices();
                return;
            }

            _activeSourceIndex = -1;
            _transition = StartCoroutine(FadeOutAllVoices(_crossfadeSeconds));
        }

        /// <summary>Changes only the ambience category volume, clamped to 0–1.</summary>
        public void SetAmbienceVolume(float volume)
        {
            AmbienceVolume = volume;
        }

        private void OnDisable()
        {
            CancelPendingLoad();
            CancelTransition(collapsePlayingVoices: false);
            _requestedId = null;
            StopAllVoices();
        }

        private IEnumerator WaitForClipThenPlay(string ambienceId, AudioClip clip)
        {
            while (clip != null && clip.loadState != AudioDataLoadState.Loaded &&
                   clip.loadState != AudioDataLoadState.Failed)
            {
                yield return null;
            }

            bool isCurrentLoad = isActiveAndEnabled &&
                                 string.Equals(_pendingId, ambienceId, StringComparison.Ordinal) &&
                                 _pendingClip == clip;
            _pendingLoad = null;
            _pendingClip = null;
            _pendingId = null;
            if (!isCurrentLoad)
            {
                yield break;
            }
            if (clip == null || clip.loadState != AudioDataLoadState.Loaded)
            {
                yield break;
            }

            BeginTransitionToNewSource(ambienceId, clip);
        }

        private void BeginTransitionToNewSource(string ambienceId, AudioClip clip)
        {
            EnsureSources();
            CancelTransition();
            int fromIndex = PickLoudestPlayingSource();
            if (fromIndex < 0)
            {
                StopAllVoices();
                StartVoiceImmediately(0, ambienceId, clip);
                return;
            }

            int toIndex = 1 - fromIndex;
            StopVoice(toIndex);
            AudioSource target = _sources[toIndex];
            target.clip = clip;
            target.loop = true;
            _sourceIds[toIndex] = ambienceId;
            _envelopes[toIndex] = 0f;
            target.Play();
            _requestedId = ambienceId;
            _activeSourceIndex = toIndex;

            if (_crossfadeSeconds <= 0f)
            {
                CompleteTransition(fromIndex, toIndex);
                return;
            }

            _transition = StartCoroutine(CrossfadeVoices(
                fromIndex, toIndex, _envelopes[fromIndex], _envelopes[toIndex], _crossfadeSeconds));
            ApplySourceVolumes();
        }

        private void TransitionToExistingSource(string ambienceId, int targetIndex)
        {
            EnsureSources();
            CancelTransition(collapsePlayingVoices: false);
            _requestedId = ambienceId;
            _activeSourceIndex = targetIndex;

            int otherIndex = 1 - targetIndex;
            if (!IsSourcePlaying(otherIndex))
            {
                if (_crossfadeSeconds <= 0f)
                {
                    _envelopes[targetIndex] = 1f;
                    ApplySourceVolumes();
                }
                else
                {
                    _transition = StartCoroutine(FadeInSingleVoice(
                        targetIndex, _envelopes[targetIndex], _crossfadeSeconds));
                }
                return;
            }

            if (_crossfadeSeconds <= 0f)
            {
                CompleteTransition(otherIndex, targetIndex);
                return;
            }

            _transition = StartCoroutine(CrossfadeVoices(
                otherIndex, targetIndex, _envelopes[otherIndex], _envelopes[targetIndex], _crossfadeSeconds));
        }

        private IEnumerator CrossfadeVoices(int fromIndex, int toIndex, float fromStartEnvelope,
                                            float toStartEnvelope, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                _envelopes[fromIndex] = Mathf.Lerp(fromStartEnvelope, 0f, progress);
                _envelopes[toIndex] = Mathf.Lerp(toStartEnvelope, 1f, progress);
                ApplySourceVolumes();
            }
            CompleteTransition(fromIndex, toIndex);
        }

        private IEnumerator FadeInSingleVoice(int index, float startEnvelope, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                _envelopes[index] = Mathf.Lerp(startEnvelope, 1f, Mathf.Clamp01(elapsed / duration));
                ApplySourceVolumes();
            }
            _envelopes[index] = 1f;
            _transition = null;
            ApplySourceVolumes();
        }

        private IEnumerator FadeOutAllVoices(float duration)
        {
            float firstStart = _envelopes[0];
            float secondStart = _envelopes[1];
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                float remaining = 1f - Mathf.Clamp01(elapsed / duration);
                _envelopes[0] = firstStart * remaining;
                _envelopes[1] = secondStart * remaining;
                ApplySourceVolumes();
            }
            _transition = null;
            StopAllVoices();
        }

        private void CompleteTransition(int fromIndex, int toIndex)
        {
            StopVoice(fromIndex);
            _envelopes[toIndex] = 1f;
            _activeSourceIndex = toIndex;
            _transition = null;
            ApplySourceVolumes();
        }

        private void CollapseTransition()
        {
            int keepIndex = PickLoudestPlayingSource();
            if (keepIndex < 0)
            {
                _activeSourceIndex = -1;
                return;
            }
            StopVoice(1 - keepIndex);
            _activeSourceIndex = keepIndex;
        }

        private void CancelTransition(bool collapsePlayingVoices = true)
        {
            if (_transition == null) return;
            StopCoroutine(_transition);
            _transition = null;
            if (collapsePlayingVoices) CollapseTransition();
        }

        private void CancelPendingLoad()
        {
            if (_pendingLoad != null)
            {
                StopCoroutine(_pendingLoad);
                _pendingLoad = null;
            }
            _pendingClip = null;
            _pendingId = null;
        }

        private void EnsureSources()
        {
            if (_sources != null) return;
            _sources = new AudioSource[2];
            _sourceIds = new string[2];
            _envelopes = new float[2];
            _sources[0] = CreateSource("Ambience A");
            _sources[1] = CreateSource("Ambience B");
        }

        private AudioSource CreateSource(string childName)
        {
            var sourceObject = new GameObject(childName);
            sourceObject.transform.SetParent(transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            return source;
        }

        private void StartVoiceImmediately(int index, string ambienceId, AudioClip clip)
        {
            StopVoice(index);
            AudioSource source = _sources[index];
            source.clip = clip;
            source.loop = true;
            _sourceIds[index] = ambienceId;
            _envelopes[index] = 1f;
            _activeSourceIndex = index;
            _requestedId = ambienceId;
            source.Play();
            ApplySourceVolumes();
        }

        private void StopAllVoices()
        {
            if (_sources != null)
            {
                StopVoice(0);
                StopVoice(1);
            }
            _activeSourceIndex = -1;
            _transition = null;
        }

        private void StopVoice(int index)
        {
            if (_sources == null || index < 0 || index >= _sources.Length) return;
            AudioSource source = _sources[index];
            if (source != null)
            {
                source.Stop();
                source.clip = null;
                source.loop = false;
                source.volume = 0f;
            }
            if (_sourceIds != null) _sourceIds[index] = null;
            if (_envelopes != null) _envelopes[index] = 0f;
        }

        private void ApplySourceVolumes()
        {
            if (_sources == null || _envelopes == null) return;
            for (int index = 0; index < _sources.Length; index++)
            {
                if (_sources[index] != null)
                {
                    _sources[index].volume = _envelopes[index] * _ambienceVolume;
                }
            }
        }

        private bool HasPlayingAmbience(string ambienceId)
        {
            return TryFindPlayingSource(ambienceId, out _);
        }

        private bool TryFindPlayingSource(string ambienceId, out int index)
        {
            if (_sources != null && _sourceIds != null)
            {
                for (int candidate = 0; candidate < _sources.Length; candidate++)
                {
                    if (IsSourcePlaying(candidate) &&
                        string.Equals(_sourceIds[candidate], ambienceId, StringComparison.Ordinal))
                    {
                        index = candidate;
                        return true;
                    }
                }
            }
            index = -1;
            return false;
        }

        private int PickLoudestPlayingSource()
        {
            bool firstPlaying = IsSourcePlaying(0);
            bool secondPlaying = IsSourcePlaying(1);
            if (!firstPlaying) return secondPlaying ? 1 : -1;
            if (!secondPlaying) return 0;
            if (Mathf.Abs(_envelopes[0] - _envelopes[1]) <= 0.00001f && _activeSourceIndex >= 0)
            {
                return _activeSourceIndex;
            }
            return _envelopes[0] >= _envelopes[1] ? 0 : 1;
        }

        private bool IsSourcePlaying(int index)
        {
            return _sources != null && index >= 0 && index < _sources.Length &&
                   _sources[index] != null && _sources[index].isPlaying;
        }

        private static bool TryGetResourcePath(string ambienceId, out string resourcePath)
        {
            switch (ambienceId)
            {
                case ForestCourtyardId:
                case ArchiveId:
                case ForgeId:
                    resourcePath = ResourcePrefix + ambienceId;
                    return true;
                default:
                    resourcePath = null;
                    return false;
            }
        }

        private static float ClampVolume(float value)
        {
            return float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
        }

        private static float ClampCrossfade(float value)
        {
            return float.IsNaN(value) || value < 0f
                ? 0f
                : Mathf.Clamp(value, 0f, MaximumCrossfadeSeconds);
        }
    }
}
