using Luckbound.App;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ApplicationAudioInputLifecycleTests : InputTestFixture
    {
        [Test]
        public void ARealButtonEventUnlocksTheRouterOnce()
        {
            var owner = new GameObject("Audio gesture input fixture");
            try
            {
                var router = owner.AddComponent<ApplicationAudioRouter>();
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                Assert.That(router.UserGestureObserved, Is.False);
                Press(keyboard.spaceKey);
                Assert.That(router.UserGestureObserved, Is.True);
                Release(keyboard.spaceKey);
                Press(keyboard.enterKey);
                Assert.That(router.UserGestureObserved, Is.True);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void DisablingAfterInputRuntimeReplacementDoesNotDisposeAnObsoleteObserver()
        {
            var owner = new GameObject("Audio input replacement fixture");
            try
            {
                var router = owner.AddComponent<ApplicationAudioRouter>();
                Assert.That(router.UserGestureObserved, Is.False);
                // Match the lifetime boundary between InputTestFixture tests with a persistent app.
                base.TearDown();
                base.Setup();
                owner.SetActive(false);
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
