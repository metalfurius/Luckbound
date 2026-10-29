using System.Collections;
using System.Reflection;
using Luckbound.App;
using Luckbound.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Luckbound.Tests.PlayMode
{
    public sealed class I06UiAudioRoutingTests
    {
        [Test]
        public void PauseAudioRoutesExplicitTogglesButNotFocusLoss()
        {
            float previousTimeScale = Time.timeScale;
            SimulationMode2D previousSimulationMode = Physics2D.simulationMode;
            var owner = new GameObject("I06 UI audio pause fixture");
            try
            {
                owner.AddComponent<RuntimeAudioDirector>();
                ApplicationAudioRouter router = owner.AddComponent<ApplicationAudioRouter>();
                var labelObject = new GameObject("Pause label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(owner.transform, false);
                var pause = owner.AddComponent<ExpeditionPauseController>();
                pause.Initialize(labelObject.GetComponent<Text>(), pauseOnFocusLoss: true);
                typeof(ExpeditionPauseController).GetField("_audioRouter", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(pause, router);

                Assert.That(router.UserGestureObserved, Is.False);
                Assert.That(router.PendingEffectRequestCount, Is.Zero);

                pause.TogglePause();
                Assert.That(pause.IsPaused, Is.True);
                Assert.That(PendingEffectIdAt(router, 0), Is.EqualTo(ApplicationAudioRouter.UiOpenEffectId));

                pause.TogglePause();
                Assert.That(pause.IsPaused, Is.False);
                Assert.That(PendingEffectIdAt(router, 1), Is.EqualTo(ApplicationAudioRouter.UiCloseEffectId));

                pause.HandleApplicationFocus(hasFocus: false);
                Assert.That(pause.IsPaused, Is.True);
                Assert.That(router.PendingEffectRequestCount, Is.EqualTo(2),
                    "Focus-loss pausing is an automatic state change and must stay silent.");

                pause.TogglePause();
                Assert.That(pause.IsPaused, Is.False);
                Assert.That(PendingEffectIdAt(router, 2), Is.EqualTo(ApplicationAudioRouter.UiCloseEffectId));
                Assert.That(router.UserGestureObserved, Is.False,
                    "The test intentionally keeps requests queued so it never plays audio.");
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Time.timeScale = previousTimeScale;
                Physics2D.simulationMode = previousSimulationMode;
            }
        }

        private static string PendingEffectIdAt(ApplicationAudioRouter router, int index)
        {
            FieldInfo field = typeof(ApplicationAudioRouter).GetField("_pendingEffectRequests",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var requests = (IList)field.GetValue(router);
            object request = requests[index];
            return (string)request.GetType().GetProperty("EffectId").GetValue(request, null);
        }
    }
}
