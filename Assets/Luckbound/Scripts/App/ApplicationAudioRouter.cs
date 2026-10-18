using System;
using System.Collections.Generic;
using Luckbound.Presentation.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Luckbound.App
{
    /// <summary>Routes application music, ambience, and effects after browser-safe user input.</summary>
    [DisallowMultipleComponent]
    public sealed class ApplicationAudioRouter : MonoBehaviour
    {
        public const string EnemyDeathEffectId = "enemy_death";
        public const string BossAppearEffectId = "boss_appear";
        public const string BossDeathEffectId = "boss_death";
        public const string SwordSwingEffectId = "sword_swing";
        public const string SwordImpactEffectId = "sword_impact";

        private const int MaximumPendingEffectRequests = 32;

        private RuntimeAudioDirector _audioDirector;
        private RuntimeAmbienceDirector _ambienceDirector;
        private bool _listeningForInput;
        private readonly List<PendingEffectRequest> _pendingEffectRequests =
            new List<PendingEffectRequest>(MaximumPendingEffectRequests);
        private string _requestedMusicTrackId;
        private string _sceneAmbienceId;
        private string _roomAmbienceId;
        private string _currentSceneName;
        private bool _hasRoomAmbienceOverride;
        private bool _userGestureObserved;

        public string RequestedMusicTrackId => _requestedMusicTrackId;
        public string RequestedAmbienceId => _hasRoomAmbienceOverride ? _roomAmbienceId : _sceneAmbienceId;
        public bool HasRoomAmbienceOverride => _hasRoomAmbienceOverride;
        public bool UserGestureObserved => _userGestureObserved;
        public int PendingEffectRequestCount => _pendingEffectRequests.Count;

        private void Awake()
        {
            _audioDirector = GetComponent<RuntimeAudioDirector>();
            _ambienceDirector = GetComponent<RuntimeAmbienceDirector>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (!_userGestureObserved)
            {
                InputSystem.onEvent += OnInputEvent;
                _listeningForInput = true;
            }
            SetSceneContext(SceneManager.GetActiveScene().name);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StopListeningForInput();
            _pendingEffectRequests.Clear();
        }

        public bool SetSceneContext(string sceneName)
        {
            if (!string.Equals(_currentSceneName, sceneName, StringComparison.Ordinal))
                _pendingEffectRequests.Clear();
            _currentSceneName = sceneName;
            bool hasMusic = TryResolveMusicTrack(sceneName, out _requestedMusicTrackId);
            TryResolveAmbience(sceneName, out _sceneAmbienceId);
            ApplyRequestedMusic();
            ApplyRequestedAmbience();
            return hasMusic;
        }

        /// <summary>
        /// Gives the active expedition room priority over the scene ambience. A null
        /// value is an explicit silent room and still suppresses the scene fallback.
        /// Playback remains behind the same first-input gate as scene ambience.
        /// </summary>
        public void SetRoomAmbienceOverride(string ambienceId)
        {
            _roomAmbienceId = ambienceId;
            _hasRoomAmbienceOverride = true;
            ApplyRequestedAmbience();
        }

        /// <summary>Releases the active room request and restores the current scene fallback.</summary>
        public void ClearRoomAmbienceOverride()
        {
            if (!_hasRoomAmbienceOverride) return;
            _roomAmbienceId = null;
            _hasRoomAmbienceOverride = false;
            ApplyRequestedAmbience();
        }

        /// <summary>Call from a real or simulated input event to unlock WebGL audio playback.</summary>
        public void NotifyUserGesture()
        {
            if (_userGestureObserved) return;
            _userGestureObserved = true;
            StopListeningForInput();
            ApplyRequestedMusic();
            ApplyRequestedAmbience();
            FlushPendingEffectRequests();
        }

        /// <summary>
        /// Routes a one-shot through the persistent director. Requests made before the first
        /// input are held only for this scene and flushed when Input System unlocks playback.
        /// </summary>
        public bool PlayEffect(string effectId, float gain = 1f)
        {
            if (!isActiveAndEnabled || _audioDirector == null || string.IsNullOrWhiteSpace(effectId)) return false;
            if (_userGestureObserved) return _audioDirector.PlayEffect(effectId, gain);
            if (_pendingEffectRequests.Count >= MaximumPendingEffectRequests) return false;

            _pendingEffectRequests.Add(new PendingEffectRequest(effectId, gain));
            return true;
        }

        public static bool TryResolveMusicTrack(string sceneName, out string trackId)
        {
            switch (sceneName)
            {
                case "Hub":
                    trackId = RuntimeAudioDirector.TavernTrackId;
                    return true;
                case "Expedition":
                case "CombatArena":
                    trackId = RuntimeAudioDirector.PatioTrackId;
                    return true;
                case "CustodianArena":
                    trackId = RuntimeAudioDirector.CustodianTrackId;
                    return true;
                default:
                    trackId = null;
                    return false;
            }
        }

        public static bool TryResolveAmbience(string sceneName, out string ambienceId)
        {
            switch (sceneName)
            {
                case "Expedition":
                case "CombatArena":
                    ambienceId = RuntimeAmbienceDirector.ForestCourtyardId;
                    return true;
                default:
                    ambienceId = null;
                    return false;
            }
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if ((eventPtr.IsA<StateEvent>() || eventPtr.IsA<DeltaStateEvent>()) &&
                eventPtr.GetFirstButtonPressOrNull() != null)
                NotifyUserGesture();
        }

        private void StopListeningForInput()
        {
            if (!_listeningForInput) return;
            InputSystem.onEvent -= OnInputEvent;
            _listeningForInput = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode _)
        {
            // A reload of the same named scene is a new lifetime too.
            _pendingEffectRequests.Clear();
            _currentSceneName = scene.name;
            SetSceneContext(scene.name);
        }

        private void FlushPendingEffectRequests()
        {
            if (_audioDirector == null || !_audioDirector.isActiveAndEnabled || _pendingEffectRequests.Count == 0) return;

            for (int index = 0; index < _pendingEffectRequests.Count; index++)
            {
                PendingEffectRequest request = _pendingEffectRequests[index];
                _audioDirector.PlayEffect(request.EffectId, request.Gain);
            }
            _pendingEffectRequests.Clear();
        }

        private void ApplyRequestedMusic()
        {
            if (!_userGestureObserved || _audioDirector == null) return;
            if (string.IsNullOrEmpty(_requestedMusicTrackId))
            {
                _audioDirector.StopMusic();
                return;
            }

            _audioDirector.PlayMusic(_requestedMusicTrackId);
        }

        private void ApplyRequestedAmbience()
        {
            if (!_userGestureObserved || _ambienceDirector == null) return;
            string ambienceId = RequestedAmbienceId;
            if (string.IsNullOrEmpty(ambienceId))
            {
                _ambienceDirector.StopAmbience();
                return;
            }

            _ambienceDirector.PlayAmbience(ambienceId);
        }

        private readonly struct PendingEffectRequest
        {
            public PendingEffectRequest(string effectId, float gain)
            {
                EffectId = effectId;
                Gain = gain;
            }

            public string EffectId { get; }
            public float Gain { get; }
        }
    }
}
