using System;
using Luckbound.Gameplay.Rooms;
using Luckbound.Presentation.Audio;
using UnityEngine;

namespace Luckbound.App
{
    /// <summary>Routes room activation events to the ambience director owned by the application.</summary>
    public sealed class ExpeditionRoomAmbienceRouter : MonoBehaviour, IDisposable
    {
        private const string PatioTemplatePrefix = "room.patio.";
        private const string ArchiveTemplatePrefix = "room.archive.";
        private const string ArchivoTemplatePrefix = "room.archivo.";
        private const string ForgeTemplatePrefix = "room.forge.";
        private const string ForjasTemplatePrefix = "room.forjas.";

        private ExpeditionRoomTransitionController _roomTransitions;
        private RuntimeAmbienceDirector _ambienceDirector;
        private bool _disposed;

        public string CurrentRoomTemplateId { get; private set; }
        public string CurrentAmbienceId { get; private set; }

        /// <summary>
        /// Connects this router to one explicit room transition source and ambience director.
        /// Rebinding the same pair is a no-op; replacing the pair releases the previous request.
        /// </summary>
        public void Bind(ExpeditionRoomTransitionController roomTransitions, RuntimeAmbienceDirector ambienceDirector)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ExpeditionRoomAmbienceRouter));
            if (roomTransitions == null) throw new ArgumentNullException(nameof(roomTransitions));
            if (ambienceDirector == null) throw new ArgumentNullException(nameof(ambienceDirector));

            if (ReferenceEquals(_roomTransitions, roomTransitions) && ReferenceEquals(_ambienceDirector, ambienceDirector))
            {
                return;
            }

            Unbind(stopAmbience: true);
            _roomTransitions = roomTransitions;
            _ambienceDirector = ambienceDirector;
            _roomTransitions.RoomActivated += OnRoomActivated;

            // Binding can happen after initialization; reflect the current room as well as later events.
            if (_roomTransitions.ActiveScope != null)
            {
                OnRoomActivated(_roomTransitions.ActiveScope);
            }
        }

        /// <summary>Maps stable room template IDs to their loopable ambience resource IDs.</summary>
        public static string ResolveAmbienceId(string templateId)
        {
            if (string.IsNullOrEmpty(templateId)) return null;
            if (templateId.StartsWith(PatioTemplatePrefix, StringComparison.Ordinal))
                return RuntimeAmbienceDirector.ForestCourtyardId;
            if (templateId.StartsWith(ArchiveTemplatePrefix, StringComparison.Ordinal) ||
                templateId.StartsWith(ArchivoTemplatePrefix, StringComparison.Ordinal))
                return RuntimeAmbienceDirector.ArchiveId;
            if (templateId.StartsWith(ForgeTemplatePrefix, StringComparison.Ordinal) ||
                templateId.StartsWith(ForjasTemplatePrefix, StringComparison.Ordinal))
                return RuntimeAmbienceDirector.ForgeId;
            return null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Unbind(stopAmbience: true);
        }

        private void OnRoomActivated(RoomLifetimeScope room)
        {
            if (_disposed || _ambienceDirector == null) return;

            CurrentRoomTemplateId = room == null || room.IsDisposed ? null : room.TemplateId;
            string ambienceId = ResolveAmbienceId(CurrentRoomTemplateId);
            if (ambienceId == null || !_ambienceDirector.PlayAmbience(ambienceId))
            {
                _ambienceDirector.StopAmbience();
                CurrentAmbienceId = null;
                return;
            }

            CurrentAmbienceId = ambienceId;
        }

        private void Unbind(bool stopAmbience)
        {
            ExpeditionRoomTransitionController previousTransitions = _roomTransitions;
            RuntimeAmbienceDirector previousDirector = _ambienceDirector;
            _roomTransitions = null;
            _ambienceDirector = null;

            if (!ReferenceEquals(previousTransitions, null))
            {
                previousTransitions.RoomActivated -= OnRoomActivated;
            }

            CurrentRoomTemplateId = null;
            CurrentAmbienceId = null;
            if (stopAmbience && previousDirector != null)
            {
                previousDirector.StopAmbience();
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
