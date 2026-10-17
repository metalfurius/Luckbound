using System;
using Luckbound.Gameplay.Rooms;
using Luckbound.Presentation.Audio;
using UnityEngine;

namespace Luckbound.App
{
    /// <summary>Routes room activation events through the application audio request seam.</summary>
    public sealed class ExpeditionRoomAmbienceRouter : MonoBehaviour, IDisposable
    {
        private const string PatioTemplatePrefix = "room.patio.";
        private const string ArchiveTemplatePrefix = "room.archive.";
        private const string ArchivoTemplatePrefix = "room.archivo.";
        private const string ForgeTemplatePrefix = "room.forge.";
        private const string ForjasTemplatePrefix = "room.forjas.";

        private ExpeditionRoomTransitionController _roomTransitions;
        private ApplicationAudioRouter _audioRouter;
        private bool _disposed;

        public string CurrentRoomTemplateId { get; private set; }
        public string CurrentAmbienceId { get; private set; }
        public bool IsDisposed => _disposed;

        /// <summary>
        /// Connects this router to one explicit room transition source and application audio router.
        /// Rebinding the same pair is a no-op; replacing the pair releases the previous request.
        /// </summary>
        public void Bind(ExpeditionRoomTransitionController roomTransitions, ApplicationAudioRouter audioRouter)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ExpeditionRoomAmbienceRouter));
            if (roomTransitions == null) throw new ArgumentNullException(nameof(roomTransitions));
            if (audioRouter == null) throw new ArgumentNullException(nameof(audioRouter));

            if (ReferenceEquals(_roomTransitions, roomTransitions) && ReferenceEquals(_audioRouter, audioRouter))
            {
                return;
            }

            Unbind(clearOverride: true);
            _roomTransitions = roomTransitions;
            _audioRouter = audioRouter;
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
            Unbind(clearOverride: true);
        }

        /// <summary>Detaches the current room source and restores scene ambience; the router remains reusable.</summary>
        public void Unbind()
        {
            if (_disposed) return;
            Unbind(clearOverride: true);
        }

        private void OnRoomActivated(RoomLifetimeScope room)
        {
            if (_disposed || _audioRouter == null) return;

            CurrentRoomTemplateId = room == null || room.IsDisposed ? null : room.TemplateId;
            CurrentAmbienceId = ResolveAmbienceId(CurrentRoomTemplateId);
            _audioRouter.SetRoomAmbienceOverride(CurrentAmbienceId);
        }

        private void Unbind(bool clearOverride)
        {
            ExpeditionRoomTransitionController previousTransitions = _roomTransitions;
            ApplicationAudioRouter previousAudioRouter = _audioRouter;
            _roomTransitions = null;
            _audioRouter = null;

            if (!ReferenceEquals(previousTransitions, null))
            {
                previousTransitions.RoomActivated -= OnRoomActivated;
            }

            CurrentRoomTemplateId = null;
            CurrentAmbienceId = null;
            if (clearOverride && previousAudioRouter != null)
            {
                previousAudioRouter.ClearRoomAmbienceOverride();
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
