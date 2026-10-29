#if UNITY_EDITOR
using System;
using System.Collections;
using Luckbound.App;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ExpeditionSimulationPauseLeaseTests
    {
        private GameObject _owner;
        private ExpeditionPauseController _controller;
        private Text _label;
        private float _previousTimeScale;
        private SimulationMode2D _previousPhysicsMode;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previousTimeScale = Time.timeScale;
            _previousPhysicsMode = Physics2D.simulationMode;
            Time.timeScale = 0.75f;
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;

            _owner = new GameObject("ExpeditionSimulationPauseLeaseTest");
            GameObject labelObject = new GameObject("PauseLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(_owner.transform, false);
            _label = labelObject.GetComponent<Text>();
            _controller = _owner.AddComponent<ExpeditionPauseController>();
            _controller.Initialize(_label);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_owner != null) UnityEngine.Object.Destroy(_owner);
            yield return null;
            Time.timeScale = _previousTimeScale;
            Physics2D.simulationMode = _previousPhysicsMode;
        }

        [UnityTest]
        public IEnumerator LeaseStopsSimulationWithoutChangingManualPauseStateOrEvents()
        {
            int pauseChangedCount = 0;
            _controller.PauseChanged += _ => pauseChangedCount++;

            IDisposable lease = _controller.AcquireSimulationPauseLease();

            Assert.That(_controller.IsPaused, Is.False);
            Assert.That(_controller.IsSimulationPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Physics2D.simulationMode, Is.EqualTo(SimulationMode2D.Script));
            Assert.That(_label.text, Is.Empty);
            Assert.That(pauseChangedCount, Is.Zero);

            lease.Dispose();
            lease.Dispose();

            AssertSimulationRestored();
            Assert.That(_controller.IsPaused, Is.False);
            Assert.That(_controller.IsSimulationPaused, Is.False);
            Assert.That(pauseChangedCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NestedLeasesKeepSimulationStoppedUntilTheLastLeaseIsDisposed()
        {
            IDisposable first = _controller.AcquireSimulationPauseLease();
            IDisposable second = _controller.AcquireSimulationPauseLease();

            first.Dispose();
            first.Dispose();
            Assert.That(_controller.IsSimulationPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Physics2D.simulationMode, Is.EqualTo(SimulationMode2D.Script));

            second.Dispose();

            AssertSimulationRestored();
            Assert.That(_controller.IsSimulationPaused, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ManualPauseAndLeaseCanBeReleasedInEitherOrder()
        {
            int pauseChangedCount = 0;
            _controller.PauseChanged += _ => pauseChangedCount++;

            _controller.SetPaused(true);
            IDisposable leaseWhileManuallyPaused = _controller.AcquireSimulationPauseLease();
            _controller.SetPaused(false);

            Assert.That(_controller.IsPaused, Is.False);
            Assert.That(_controller.IsSimulationPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(_label.text, Is.Empty);
            Assert.That(pauseChangedCount, Is.EqualTo(2));

            leaseWhileManuallyPaused.Dispose();
            AssertSimulationRestored();

            IDisposable leaseBeforeManualPause = _controller.AcquireSimulationPauseLease();
            _controller.SetPaused(true);
            leaseBeforeManualPause.Dispose();

            Assert.That(_controller.IsPaused, Is.True);
            Assert.That(_controller.IsSimulationPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            StringAssert.Contains("PAUSA", _label.text);
            Assert.That(pauseChangedCount, Is.EqualTo(3));

            _controller.SetPaused(false);
            AssertSimulationRestored();
            Assert.That(pauseChangedCount, Is.EqualTo(4));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyingControllerRestoresSimulationAndInvalidatesOutstandingLeases()
        {
            IDisposable lease = _controller.AcquireSimulationPauseLease();
            UnityEngine.Object.Destroy(_owner);
            _owner = null;
            yield return null;

            AssertSimulationRestored();
            Assert.DoesNotThrow(() => lease.Dispose());
            Assert.DoesNotThrow(() => lease.Dispose());
        }

        private void AssertSimulationRestored()
        {
            Assert.That(Time.timeScale, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(Physics2D.simulationMode, Is.EqualTo(SimulationMode2D.FixedUpdate));
        }
    }
}
#endif
