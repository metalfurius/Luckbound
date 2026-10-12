using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Core.Identifiers;
using UnityEngine;

namespace Luckbound.Gameplay.Rooms
{
    /// <summary>Owns room-local actors, coroutines, subscriptions and disposable services.</summary>
    public sealed class RoomLifetimeScope : MonoBehaviour, IDisposable
    {
        private readonly List<GameObject> _actors = new List<GameObject>();
        private readonly List<IDisposable> _resources = new List<IDisposable>();
        private readonly HashSet<int> _runningCoroutines = new HashSet<int>();
        private bool _initialized;
        private bool _disposed;
        private int _nextCoroutineId;

        public RoomInstanceId RoomInstanceId { get; private set; }
        public string TemplateId { get; private set; }
        public bool IsDisposed => _disposed;
        public int OwnedActorCount => _actors.Count(actor => actor != null && actor.activeInHierarchy);
        public int OwnedResourceCount => _resources.Count;
        public int ActiveCoroutineCount => _runningCoroutines.Count;

        public void Initialize(RoomInstanceId roomInstanceId, string templateId)
        {
            if (_initialized) throw new InvalidOperationException("RoomLifetimeScope can only be initialized once.");
            if (!roomInstanceId.IsValid) throw new ArgumentException("Room lifetime requires a valid room instance ID.", nameof(roomInstanceId));
            if (string.IsNullOrWhiteSpace(templateId)) throw new ArgumentException("Room lifetime requires a stable template ID.", nameof(templateId));

            RoomInstanceId = roomInstanceId;
            TemplateId = templateId;
            _initialized = true;
        }

        public GameObject OwnActor(GameObject actor)
        {
            EnsureActive();
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (actor == gameObject || _actors.Contains(actor)) throw new InvalidOperationException("Room actors must be distinct child objects.");

            actor.transform.SetParent(transform, true);
            _actors.Add(actor);
            return actor;
        }

        public T OwnResource<T>(T resource) where T : IDisposable
        {
            EnsureActive();
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (_resources.Contains(resource)) throw new InvalidOperationException("A room resource cannot be registered more than once.");

            _resources.Add(resource);
            return resource;
        }

        public T OwnSubscription<T>(T subscription) where T : IDisposable
        {
            return OwnResource(subscription);
        }

        public Coroutine StartOwnedCoroutine(IEnumerator routine)
        {
            EnsureActive();
            if (routine == null) throw new ArgumentNullException(nameof(routine));

            int coroutineId = ++_nextCoroutineId;
            _runningCoroutines.Add(coroutineId);
            return StartCoroutine(TrackCoroutine(coroutineId, routine));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopAllCoroutines();
            _runningCoroutines.Clear();

            IDisposable[] resources = _resources.ToArray();
            _resources.Clear();
            for (int index = resources.Length - 1; index >= 0; index--)
            {
                try
                {
                    resources[index].Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }

            GameObject[] actors = _actors.ToArray();
            _actors.Clear();
            foreach (GameObject actor in actors)
            {
                if (actor == null) continue;
                actor.SetActive(false);
                Destroy(actor);
            }
        }

        private IEnumerator TrackCoroutine(int coroutineId, IEnumerator routine)
        {
            try
            {
                while (!_disposed && routine.MoveNext()) yield return routine.Current;
            }
            finally
            {
                _runningCoroutines.Remove(coroutineId);
                (routine as IDisposable)?.Dispose();
            }
        }

        private void EnsureActive()
        {
            if (!_initialized || _disposed || !isActiveAndEnabled)
            {
                throw new InvalidOperationException("Room resources can only be registered by an initialized active room.");
            }
        }

        private void OnDisable()
        {
            if (_initialized) Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
