using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Luckbound.Presentation.Audio
{
    /// <summary>
    /// Scene-owned audio playback for the currently shipped F08 music and effect catalogs.
    /// Add one instance to the scene that owns playback; it does not persist or create a global singleton.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeAudioDirector : MonoBehaviour
    {
        public const string TavernTrackId = "tavern_theme";
        public const string PatioTrackId = "patio_theme";
        public const string CustodianTrackId = "custodian_theme";

        private const string EffectCatalogPath = "Luckbound/Data/AudioEffects";
        private const string MusicCatalogPath = "Luckbound/Data/AudioMusic";
        private const string EffectResourcePrefix = "Luckbound/Audio/Effects/";
        private const string MusicResourcePrefix = "Luckbound/Audio/Music/";
        private const int RequiredEffectCount = 36;
        private const int RequiredMusicTrackCount = 3;
        private const int DefaultMaximumOneShotSources = 12;
        private const float DefaultMusicCrossfadeSeconds = 0.8f;

        [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.65f;
        [SerializeField, Range(0f, 1f)] private float _effectsVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _ambienceVolume = 0.45f;
        [SerializeField, Min(0f)] private float _musicCrossfadeSeconds = DefaultMusicCrossfadeSeconds;
        [SerializeField, Range(1, 32)] private int _maximumOneShotSources = DefaultMaximumOneShotSources;

        private readonly Dictionary<string, EffectDefinition> _effects =
            new Dictionary<string, EffectDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, MusicDefinition> _music =
            new Dictionary<string, MusicDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> _effectClipCache =
            new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly List<EffectVoice> _effectVoices = new List<EffectVoice>();

        private AudioSource[] _musicSources;
        private string[] _musicSourceTrackIds;
        private float[] _musicEnvelopes;
        private Coroutine _musicTransition;
        private Coroutine _musicLoadCoroutine;
        private AudioClip _pendingMusicClip;
        private string _pendingMusicId;
        private string _requestedMusicId;
        private int _activeMusicIndex = -1;
        private bool _effectCatalogLoaded;
        private bool _musicCatalogLoaded;
        private bool _effectCatalogAttempted;
        private bool _musicCatalogAttempted;

        public float MasterVolume => _masterVolume;
        public float MusicVolume => _musicVolume;
        public float EffectsVolume => _effectsVolume;
        public float AmbienceVolume => _ambienceVolume;
        public float EffectiveMusicVolume => _masterVolume * _musicVolume;
        public float EffectiveEffectsVolume => _masterVolume * _effectsVolume;
        public float EffectiveAmbienceVolume => _masterVolume * _ambienceVolume;

        public int EffectCatalogCount
        {
            get
            {
                EnsureEffectCatalog();
                return _effects.Count;
            }
        }

        public int MusicCatalogCount
        {
            get
            {
                EnsureMusicCatalog();
                return _music.Count;
            }
        }

        public int MusicSourceCount => _musicSources == null ? 0 : _musicSources.Length;
        public int OneShotSourceCount => _effectVoices.Count;
        public string ActiveMusicTrackId => _musicSourceTrackIds == null || _activeMusicIndex < 0 ||
            _activeMusicIndex >= _musicSourceTrackIds.Length ? null : _musicSourceTrackIds[_activeMusicIndex];

        public int ActiveMusicSourceCount
        {
            get
            {
                if (_musicSources == null) return 0;
                int count = 0;
                for (int index = 0; index < _musicSources.Length; index++)
                {
                    if (_musicSources[index] != null && _musicSources[index].isPlaying) count++;
                }
                return count;
            }
        }

        public int ActiveOneShotSourceCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _effectVoices.Count; index++)
                {
                    if (_effectVoices[index].Source != null && _effectVoices[index].Source.isPlaying) count++;
                }
                return count;
            }
        }

        public int MaximumOneShotSources
        {
            get => _maximumOneShotSources;
            set
            {
                _maximumOneShotSources = Mathf.Clamp(value, 1, 32);
                while (_effectVoices.Count > _maximumOneShotSources)
                {
                    int last = _effectVoices.Count - 1;
                    AudioSource source = _effectVoices[last].Source;
                    if (source != null)
                    {
                        source.Stop();
                        Destroy(source.gameObject);
                    }
                    _effectVoices.RemoveAt(last);
                }
            }
        }

        public float MusicCrossfadeSeconds
        {
            get => _musicCrossfadeSeconds;
            set => _musicCrossfadeSeconds = ClampNonNegative(value);
        }

        /// <summary>Applies settings as master × category, with each input clamped to 0–1.</summary>
        public void SetVolumes(float master, float music, float effects, float ambience)
        {
            _masterVolume = ClampVolume(master);
            _musicVolume = ClampVolume(music);
            _effectsVolume = ClampVolume(effects);
            _ambienceVolume = ClampVolume(ambience);
            ApplySourceVolumes();
        }

        /// <summary>
        /// Starts one of the cataloged tracks. Repeating the current request is idempotent.
        /// A new request crossfades from the currently loudest scene-owned music source.
        /// </summary>
        public bool PlayMusic(string trackId)
        {
            if (string.IsNullOrEmpty(trackId) || !EnsureMusicCatalog() || !_music.TryGetValue(trackId, out MusicDefinition definition))
            {
                return false;
            }

            if (string.Equals(_pendingMusicId, trackId, StringComparison.Ordinal) && _musicLoadCoroutine != null)
            {
                return true;
            }
            if (string.Equals(_requestedMusicId, trackId, StringComparison.Ordinal) && HasPlayingTrack(trackId))
            {
                CancelPendingMusicLoad();
                return true;
            }

            AudioClip clip = Resources.Load<AudioClip>(definition.ResourcePath);
            if (clip == null) return false;
            CancelPendingMusicLoad();
            if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData() &&
                clip.loadState != AudioDataLoadState.Loaded && clip.loadState != AudioDataLoadState.Loading)
            {
                return false;
            }
            if (clip.loadState == AudioDataLoadState.Failed) return false;

            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                _pendingMusicId = trackId;
                _pendingMusicClip = clip;
                _musicLoadCoroutine = StartCoroutine(WaitForMusicLoadThenPlay(trackId, definition, clip));
                return true;
            }

            BeginMusicTransition(trackId, definition, clip);
            return true;
        }

        private void BeginMusicTransition(string trackId, MusicDefinition definition, AudioClip clip)
        {
            EnsureMusicSources();
            if (_musicTransition != null)
            {
                StopCoroutine(_musicTransition);
                _musicTransition = null;
                CollapseMusicTransition();
            }

            if (_activeMusicIndex < 0 || !IsSourcePlaying(_activeMusicIndex))
            {
                StopAllMusicVoices();
                StartMusicImmediately(0, trackId, clip);
                return;
            }

            int fromIndex = _activeMusicIndex;
            int toIndex = 1 - fromIndex;
            StopMusicVoice(toIndex);
            AudioSource target = _musicSources[toIndex];
            target.clip = clip;
            target.loop = definition.Loop;
            target.volume = 0f;
            _musicSourceTrackIds[toIndex] = trackId;
            _musicEnvelopes[toIndex] = 0f;
            target.Play();

            float fromEnvelope = _musicEnvelopes[fromIndex];
            _activeMusicIndex = toIndex;
            _requestedMusicId = trackId;
            float duration = ClampNonNegative(_musicCrossfadeSeconds);
            if (duration <= 0f)
            {
                CompleteMusicCrossfade(fromIndex, toIndex);
            }
            else
            {
                _musicTransition = StartCoroutine(CrossfadeMusic(fromIndex, toIndex, fromEnvelope, duration));
            }
            ApplySourceVolumes();
        }

        /// <summary>Fades out and releases every music voice owned by this scene instance.</summary>
        public void StopMusic()
        {
            CancelPendingMusicLoad();
            _requestedMusicId = null;
            if (_musicSources == null) return;

            if (_musicTransition != null)
            {
                StopCoroutine(_musicTransition);
                _musicTransition = null;
            }

            bool anyPlaying = false;
            for (int index = 0; index < _musicSources.Length; index++)
            {
                anyPlaying |= IsSourcePlaying(index);
            }
            if (!anyPlaying)
            {
                StopAllMusicVoices();
                return;
            }

            float duration = ClampNonNegative(_musicCrossfadeSeconds);
            if (duration <= 0f)
            {
                StopAllMusicVoices();
                return;
            }

            _activeMusicIndex = -1;
            _musicTransition = StartCoroutine(FadeOutMusic(duration));
        }

        /// <summary>
        /// Plays a cataloged one-shot at a deterministic pitch. If all capped voices are busy,
        /// the request is rejected instead of allocating or interrupting another sound.
        /// </summary>
        public bool PlayEffect(string effectId, float gain = 1f)
        {
            if (string.IsNullOrEmpty(effectId) || !EnsureEffectCatalog() || !_effects.TryGetValue(effectId, out EffectDefinition definition))
            {
                return false;
            }

            if (!_effectClipCache.TryGetValue(effectId, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>(definition.ResourcePath);
                if (clip == null) return false;
                _effectClipCache.Add(effectId, clip);
            }

            EffectVoice voice = FindAvailableEffectVoice();
            if (voice == null)
            {
                if (_effectVoices.Count >= _maximumOneShotSources) return false;
                voice = CreateEffectVoice();
            }

            voice.Source.Stop();
            voice.Source.clip = clip;
            voice.Source.loop = false;
            voice.Source.pitch = 1f;
            voice.Gain = ClampVolume(gain);
            voice.Source.volume = EffectiveEffectsVolume * voice.Gain;
            voice.Source.Play();
            return true;
        }

        private void Awake()
        {
            SetVolumes(_masterVolume, _musicVolume, _effectsVolume, _ambienceVolume);
            _maximumOneShotSources = Mathf.Clamp(_maximumOneShotSources, 1, 32);
            _musicCrossfadeSeconds = ClampNonNegative(_musicCrossfadeSeconds);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _musicLoadCoroutine = null;
            _pendingMusicId = null;
            if (_pendingMusicClip != null && !IsMusicClipPlaying(_pendingMusicClip))
            {
                UnloadLoadedMusicData(_pendingMusicClip);
            }
            _pendingMusicClip = null;
            _musicTransition = null;
            _requestedMusicId = null;
            _activeMusicIndex = -1;
            if (_musicSources != null)
            {
                for (int index = 0; index < _musicSources.Length; index++) StopMusicVoice(index);
            }
            for (int index = 0; index < _effectVoices.Count; index++)
            {
                AudioSource source = _effectVoices[index].Source;
                if (source == null) continue;
                source.Stop();
                source.clip = null;
                _effectVoices[index].Gain = 0f;
            }
        }

        private void OnDestroy()
        {
            ReleaseCachedMusicData();
            _effectClipCache.Clear();
        }

        private IEnumerator WaitForMusicLoadThenPlay(string trackId, MusicDefinition definition, AudioClip clip)
        {
            yield return null;
            while (clip != null && clip.loadState != AudioDataLoadState.Loaded &&
                   clip.loadState != AudioDataLoadState.Failed)
            {
                yield return null;
            }

            bool requestIsCurrent = string.Equals(_pendingMusicId, trackId, StringComparison.Ordinal);
            _musicLoadCoroutine = null;
            _pendingMusicId = null;
            _pendingMusicClip = null;
            if (clip == null || clip.loadState != AudioDataLoadState.Loaded)
            {
                if (!IsMusicClipPlaying(clip)) UnloadLoadedMusicData(clip);
                yield break;
            }
            if (!requestIsCurrent || !isActiveAndEnabled)
            {
                if (!IsMusicClipPlaying(clip)) UnloadLoadedMusicData(clip);
                yield break;
            }

            BeginMusicTransition(trackId, definition, clip);
        }

        private void CancelPendingMusicLoad()
        {
            if (_musicLoadCoroutine != null)
            {
                StopCoroutine(_musicLoadCoroutine);
                _musicLoadCoroutine = null;
            }
            if (_pendingMusicClip != null && !IsMusicClipPlaying(_pendingMusicClip))
            {
                UnloadLoadedMusicData(_pendingMusicClip);
            }
            _pendingMusicClip = null;
            _pendingMusicId = null;
        }

        private static void UnloadLoadedMusicData(AudioClip clip)
        {
            // Unity logs a warning when UnloadAudioData races an async background load.
            // Pending loads cannot be cancelled; only release audio that has fully loaded.
            if (clip != null && clip.loadState == AudioDataLoadState.Loaded)
                clip.UnloadAudioData();
        }

        private bool IsMusicClipPlaying(AudioClip clip)
        {
            if (_musicSources == null || clip == null) return false;
            for (int index = 0; index < _musicSources.Length; index++)
            {
                if (_musicSources[index] != null && _musicSources[index].isPlaying && _musicSources[index].clip == clip)
                {
                    return true;
                }
            }
            return false;
        }

        private bool EnsureEffectCatalog()
        {
            if (_effectCatalogAttempted) return _effectCatalogLoaded;
            _effectCatalogAttempted = true;
            TextAsset json = Resources.Load<TextAsset>(EffectCatalogPath);
            if (json == null) return false;

            EffectCatalogDocument catalog;
            try
            {
                catalog = JsonUtility.FromJson<EffectCatalogDocument>(json.text);
            }
            catch (ArgumentException)
            {
                return false;
            }
            if (catalog == null || catalog.schema_version != 1 || catalog.catalog_id != "audio.effects" || catalog.effects == null ||
                catalog.effects.Length != RequiredEffectCount)
            {
                return false;
            }

            for (int index = 0; index < catalog.effects.Length; index++)
            {
                EffectCatalogEntry entry = catalog.effects[index];
                if (entry == null || string.IsNullOrEmpty(entry.id) || _effects.ContainsKey(entry.id) ||
                    !TryMakeResourceKey(entry.asset_path, EffectResourcePrefix, out string resourcePath) ||
                    !string.Equals(resourcePath, EffectResourcePrefix + entry.id, StringComparison.Ordinal))
                {
                    _effects.Clear();
                    return false;
                }
                _effects.Add(entry.id, new EffectDefinition(resourcePath));
            }
            _effectCatalogLoaded = _effects.Count == RequiredEffectCount;
            return _effectCatalogLoaded;
        }

        private bool EnsureMusicCatalog()
        {
            if (_musicCatalogAttempted) return _musicCatalogLoaded;
            _musicCatalogAttempted = true;
            TextAsset json = Resources.Load<TextAsset>(MusicCatalogPath);
            if (json == null) return false;

            MusicCatalogDocument catalog;
            try
            {
                catalog = JsonUtility.FromJson<MusicCatalogDocument>(json.text);
            }
            catch (ArgumentException)
            {
                return false;
            }
            if (catalog == null || catalog.schema_version != 1 || catalog.catalog_id != "audio.music.f08" || catalog.tracks == null ||
                catalog.tracks.Length != RequiredMusicTrackCount)
            {
                return false;
            }

            for (int index = 0; index < catalog.tracks.Length; index++)
            {
                MusicCatalogEntry entry = catalog.tracks[index];
                if (entry == null || !IsKnownTrackId(entry.id) || _music.ContainsKey(entry.id) || !entry.loop ||
                    string.IsNullOrEmpty(entry.resource_path) ||
                    !entry.resource_path.StartsWith(MusicResourcePrefix, StringComparison.Ordinal) ||
                    entry.resource_path.Contains("..") || entry.resource_path.Contains("\\") ||
                    !string.Equals(entry.resource_path, MusicResourcePrefix + entry.id, StringComparison.Ordinal))
                {
                    _music.Clear();
                    return false;
                }
                _music.Add(entry.id, new MusicDefinition(entry.resource_path, entry.loop));
            }
            _musicCatalogLoaded = _music.Count == RequiredMusicTrackCount &&
                _music.ContainsKey(TavernTrackId) && _music.ContainsKey(PatioTrackId) && _music.ContainsKey(CustodianTrackId);
            return _musicCatalogLoaded;
        }

        private static bool TryMakeResourceKey(string assetPath, string resourcePrefix, out string resourcePath)
        {
            resourcePath = null;
            const string resourceFolder = "Assets/Resources/";
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith(resourceFolder, StringComparison.Ordinal) ||
                !assetPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string candidate = assetPath.Substring(resourceFolder.Length, assetPath.Length - resourceFolder.Length - 4);
            if (!candidate.StartsWith(resourcePrefix, StringComparison.Ordinal) || candidate.Contains("..") || candidate.Contains("\\"))
            {
                return false;
            }
            resourcePath = candidate;
            return true;
        }

        private static bool IsKnownTrackId(string trackId)
        {
            return string.Equals(trackId, TavernTrackId, StringComparison.Ordinal) ||
                   string.Equals(trackId, PatioTrackId, StringComparison.Ordinal) ||
                   string.Equals(trackId, CustodianTrackId, StringComparison.Ordinal);
        }

        private void EnsureMusicSources()
        {
            if (_musicSources != null) return;
            _musicSources = new AudioSource[2];
            _musicSourceTrackIds = new string[2];
            _musicEnvelopes = new float[2];
            _musicSources[0] = CreateSource("Music A");
            _musicSources[1] = CreateSource("Music B");
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

        private void StartMusicImmediately(int index, string trackId, AudioClip clip)
        {
            EnsureMusicSources();
            StopMusicVoice(index);
            AudioSource source = _musicSources[index];
            source.clip = clip;
            source.loop = _music[trackId].Loop;
            _musicSourceTrackIds[index] = trackId;
            _musicEnvelopes[index] = 1f;
            _activeMusicIndex = index;
            _requestedMusicId = trackId;
            source.Play();
            ApplySourceVolumes();
        }

        private IEnumerator CrossfadeMusic(int fromIndex, int toIndex, float fromStartEnvelope, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                _musicEnvelopes[fromIndex] = Mathf.Lerp(fromStartEnvelope, 0f, progress);
                _musicEnvelopes[toIndex] = progress;
                ApplySourceVolumes();
            }
            CompleteMusicCrossfade(fromIndex, toIndex);
        }

        private IEnumerator FadeOutMusic(float duration)
        {
            float firstStart = _musicEnvelopes[0];
            float secondStart = _musicEnvelopes[1];
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                float remaining = 1f - Mathf.Clamp01(elapsed / duration);
                _musicEnvelopes[0] = firstStart * remaining;
                _musicEnvelopes[1] = secondStart * remaining;
                ApplySourceVolumes();
            }
            _musicTransition = null;
            StopAllMusicVoices();
        }

        private void CompleteMusicCrossfade(int fromIndex, int toIndex)
        {
            _musicTransition = null;
            StopMusicVoice(fromIndex);
            _musicEnvelopes[toIndex] = 1f;
            _activeMusicIndex = toIndex;
            ApplySourceVolumes();
        }

        private void CollapseMusicTransition()
        {
            int keepIndex = PickLoudestMusicSource();
            if (keepIndex < 0)
            {
                _activeMusicIndex = -1;
                return;
            }
            int discardIndex = 1 - keepIndex;
            StopMusicVoice(discardIndex);
            _activeMusicIndex = keepIndex;
        }

        private int PickLoudestMusicSource()
        {
            bool firstPlaying = IsSourcePlaying(0);
            bool secondPlaying = IsSourcePlaying(1);
            if (!firstPlaying) return secondPlaying ? 1 : -1;
            if (!secondPlaying) return 0;
            if (Mathf.Abs(_musicEnvelopes[0] - _musicEnvelopes[1]) <= 0.00001f && _activeMusicIndex >= 0)
            {
                return _activeMusicIndex;
            }
            return _musicEnvelopes[0] >= _musicEnvelopes[1] ? 0 : 1;
        }

        private void StopAllMusicVoices()
        {
            if (_musicSources != null)
            {
                StopMusicVoice(0);
                StopMusicVoice(1);
            }
            _activeMusicIndex = -1;
            _requestedMusicId = null;
            _musicTransition = null;
        }

        private void StopMusicVoice(int index)
        {
            if (_musicSources == null || index < 0 || index >= _musicSources.Length) return;
            AudioSource source = _musicSources[index];
            AudioClip clip = source == null ? null : source.clip;
            if (source != null)
            {
                source.Stop();
                source.clip = null;
                source.loop = false;
                source.volume = 0f;
            }
            if (_musicSourceTrackIds != null) _musicSourceTrackIds[index] = null;
            if (_musicEnvelopes != null) _musicEnvelopes[index] = 0f;
            UnloadLoadedMusicData(clip);
        }

        private bool HasPlayingTrack(string trackId)
        {
            if (_musicSources == null || _musicSourceTrackIds == null) return false;
            for (int index = 0; index < _musicSources.Length; index++)
            {
                if (IsSourcePlaying(index) && string.Equals(_musicSourceTrackIds[index], trackId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private bool IsSourcePlaying(int index)
        {
            return _musicSources != null && index >= 0 && index < _musicSources.Length &&
                   _musicSources[index] != null && _musicSources[index].isPlaying;
        }

        private void ReleaseCachedMusicData()
        {
            if (_musicSources == null) return;
            for (int index = 0; index < _musicSources.Length; index++)
            {
                if (_musicSources[index] != null && _musicSources[index].clip != null)
                {
                    UnloadLoadedMusicData(_musicSources[index].clip);
                }
            }
        }

        private EffectVoice FindAvailableEffectVoice()
        {
            for (int index = 0; index < _effectVoices.Count; index++)
            {
                if (_effectVoices[index].Source != null && !_effectVoices[index].Source.isPlaying) return _effectVoices[index];
            }
            return null;
        }

        private EffectVoice CreateEffectVoice()
        {
            AudioSource source = CreateSource("Effect " + _effectVoices.Count.ToString("D2"));
            var voice = new EffectVoice(source);
            _effectVoices.Add(voice);
            return voice;
        }

        private void ApplySourceVolumes()
        {
            if (_musicSources != null && _musicEnvelopes != null)
            {
                for (int index = 0; index < _musicSources.Length; index++)
                {
                    if (_musicSources[index] != null)
                    {
                        _musicSources[index].volume = _musicEnvelopes[index] * EffectiveMusicVolume;
                    }
                }
            }
            for (int index = 0; index < _effectVoices.Count; index++)
            {
                EffectVoice voice = _effectVoices[index];
                if (voice.Source != null) voice.Source.volume = EffectiveEffectsVolume * voice.Gain;
            }
        }

        private static float ClampVolume(float value)
        {
            return float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
        }

        private static float ClampNonNegative(float value)
        {
            return float.IsNaN(value) || value < 0f ? 0f : value;
        }

        [Serializable]
        private sealed class EffectCatalogDocument
        {
            public int schema_version;
            public string catalog_id;
            public EffectCatalogEntry[] effects;
        }

        [Serializable]
        private sealed class EffectCatalogEntry
        {
            public string id;
            public string asset_path;
        }

        [Serializable]
        private sealed class MusicCatalogDocument
        {
            public int schema_version;
            public string catalog_id;
            public MusicCatalogEntry[] tracks;
        }

        [Serializable]
        private sealed class MusicCatalogEntry
        {
            public string id;
            public string resource_path;
            public bool loop;
        }

        private sealed class EffectDefinition
        {
            public EffectDefinition(string resourcePath) => ResourcePath = resourcePath;
            public string ResourcePath { get; }
        }

        private sealed class MusicDefinition
        {
            public MusicDefinition(string resourcePath, bool loop)
            {
                ResourcePath = resourcePath;
                Loop = loop;
            }

            public string ResourcePath { get; }
            public bool Loop { get; }
        }

        private sealed class EffectVoice
        {
            public EffectVoice(AudioSource source) => Source = source;
            public AudioSource Source { get; }
            public float Gain { get; set; }
        }
    }
}
