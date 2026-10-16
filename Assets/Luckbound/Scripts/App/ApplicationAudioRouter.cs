using System;
using Luckbound.Presentation.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;

namespace Luckbound.App
{
    /// <summary>Routes music on the persistent application root after browser-safe user input.</summary>
    [DisallowMultipleComponent]
    public sealed class ApplicationAudioRouter : MonoBehaviour
    {
        private RuntimeAudioDirector _audioDirector;
        private RuntimeAmbienceDirector _ambienceDirector;
        private IDisposable _inputSubscription;
        private string _requestedMusicTrackId;
        private string _requestedAmbienceId;
        private bool _userGestureObserved;

        public string RequestedMusicTrackId => _requestedMusicTrackId;
        public string RequestedAmbienceId => _requestedAmbienceId;
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
            TryResolveAmbience(sceneName, out _requestedAmbienceId);
            ApplyRequestedMusic();
            ApplyRequestedAmbience();
            return hasMusic;
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
            if (string.IsNullOrEmpty(_requestedAmbienceId))
            {
                _ambienceDirector.StopAmbience();
                return;
            }

            _ambienceDirector.PlayAmbience(_requestedAmbienceId);
        }
    }
}
