using System;
using Luckbound.Presentation.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;

namespace Luckbound.App
{
    /// <summary>Routes application music and ambience requests after browser-safe user input.</summary>
    [DisallowMultipleComponent]
    public sealed class ApplicationAudioRouter : MonoBehaviour
    {
        private RuntimeAudioDirector _audioDirector;
        private RuntimeAmbienceDirector _ambienceDirector;
        private IDisposable _inputSubscription;
        private string _requestedMusicTrackId;
        private string _sceneAmbienceId;
        private string _roomAmbienceId;
        private bool _hasRoomAmbienceOverride;
        private bool _userGestureObserved;

        public string RequestedMusicTrackId => _requestedMusicTrackId;
        public string RequestedAmbienceId => _hasRoomAmbienceOverride ? _roomAmbienceId : _sceneAmbienceId;
        public bool HasRoomAmbienceOverride => _hasRoomAmbienceOverride;
        public bool UserGestureObserved => _userGestureObserved;

        private void Awake()
        {
            _audioDirector = GetComponent<RuntimeAudioDirector>();
            _ambienceDirector = GetComponent<RuntimeAmbienceDirector>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (!_userGestureObserved)
                _inputSubscription = InputSystem.onAnyButtonPress.CallOnce(OnAnyButtonPressed);
            SetSceneContext(SceneManager.GetActiveScene().name);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _inputSubscription?.Dispose();
            _inputSubscription = null;
        }

        public bool SetSceneContext(string sceneName)
        {
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
            _inputSubscription?.Dispose();
            _inputSubscription = null;
            ApplyRequestedMusic();
            ApplyRequestedAmbience();
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

        private void OnAnyButtonPressed(InputControl _)
        {
            NotifyUserGesture();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode _)
        {
            SetSceneContext(scene.name);
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
    }
}
