using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace Luckbound.App
{
    /// <summary>Bridges WebGL virtual file-system sync callbacks into awaitable save operations.</summary>
    public sealed class WebGlFileSystemSyncer : MonoBehaviour, IPersistentFileSystemSyncer
    {
        private TaskCompletionSource<bool> _pending;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void LuckboundSyncPersistentFileSystem(string gameObjectName, string callbackMethod, int populate);
#endif

        public Task PrepareForReadAsync() => SynchronizeAsync(populate: true);
        public Task FlushAsync() => SynchronizeAsync(populate: false);

        public void CompleteFileSystemSync(string result)
        {
            TaskCompletionSource<bool> pending = _pending;
            if (pending == null) return;
            _pending = null;
            if (string.Equals(result, "ok", StringComparison.Ordinal)) pending.TrySetResult(true);
            else pending.TrySetException(new System.IO.IOException("IndexedDB synchronization failed: " + result));
        }

        private Task SynchronizeAsync(bool populate)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_pending != null) throw new InvalidOperationException("Only one IndexedDB synchronization may be active.");
            var completion = new TaskCompletionSource<bool>();
            _pending = completion;
            Task<bool> pendingTask = completion.Task;
            try
            {
                LuckboundSyncPersistentFileSystem(gameObject.name, nameof(CompleteFileSystemSync), populate ? 1 : 0);
            }
            catch (Exception exception)
            {
                _pending = null;
                throw new System.IO.IOException("Could not start IndexedDB synchronization.", exception);
            }
            return AwaitPendingAsync(pendingTask);
#else
            return Task.CompletedTask;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static async Task AwaitPendingAsync(Task<bool> pending)
        {
            await pending;
        }
#endif
    }
}
