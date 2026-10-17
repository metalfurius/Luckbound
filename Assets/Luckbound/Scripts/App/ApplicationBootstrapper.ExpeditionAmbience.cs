using System;

namespace Luckbound.App
{
    public sealed partial class ApplicationBootstrapper
    {
        /// <summary>
        /// Connects the persistent application audio root to an initialized expedition room source.
        /// Repeating this with the same controller is idempotent; binding a new controller detaches
        /// the previous room source before reflecting the new controller's active room.
        /// </summary>
        public void BindExpeditionRoomAmbience(ExpeditionRoomTransitionController roomTransitions)
        {
            if (roomTransitions == null)
                throw new ArgumentNullException(nameof(roomTransitions));
            if (roomTransitions.IsDisposed)
                throw new ArgumentException("Cannot bind ambience to a disposed room transition controller.",
                    nameof(roomTransitions));
            if (roomTransitions.ActiveScope == null)
                throw new ArgumentException("Cannot bind ambience before the transition controller activates a room.",
                    nameof(roomTransitions));

            EnsureRuntimeAudio();
            ExpeditionRoomAmbienceRouter roomAmbienceRouter = GetComponent<ExpeditionRoomAmbienceRouter>();
            if (roomAmbienceRouter == null)
                throw new InvalidOperationException("The application audio root has no room ambience router.");
            if (roomAmbienceRouter.IsDisposed)
                throw new InvalidOperationException("The application room ambience router has been disposed.");
            if (_audioRouter == null)
                throw new InvalidOperationException("The application audio root has no audio router.");

            roomAmbienceRouter.Bind(roomTransitions, _audioRouter);
        }

        /// <summary>Detaches the active room source while keeping the root router reusable.</summary>
        public void UnbindExpeditionRoomAmbience()
        {
            ExpeditionRoomAmbienceRouter roomAmbienceRouter = GetComponent<ExpeditionRoomAmbienceRouter>();
            if (roomAmbienceRouter == null) return;
            roomAmbienceRouter.Unbind();
        }
    }
}
