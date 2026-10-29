using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Luckbound.App;
using Luckbound.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Luckbound.Tests.PlayMode
{
    public sealed class I06MenuFocusAudioRoutingTests
    {
        [UnityTest]
        public IEnumerator MenuNavigationRoutesOnlyEffectivePostBaselineSelectionChanges()
        {
            EventSystem previousEventSystem = EventSystem.current;
            GameObject previousSelection = previousEventSystem == null
                ? null
                : previousEventSystem.currentSelectedGameObject;
            GameObject createdEventSystem = null;
            EventSystem eventSystem = previousEventSystem;
            var createdRoots = new List<GameObject>();

            try
            {
                if (eventSystem == null)
                {
                    createdEventSystem = new GameObject("I06 menu focus EventSystem");
                    eventSystem = createdEventSystem.AddComponent<EventSystem>();
                }
                eventSystem.SetSelectedGameObject(null);

                var audioOwner = new GameObject("I06 menu focus audio owner");
                createdRoots.Add(audioOwner);
                audioOwner.AddComponent<RuntimeAudioDirector>();
                ApplicationAudioRouter router = audioOwner.AddComponent<ApplicationAudioRouter>();

                var menuRoot = new GameObject("I06 menu navigation focus fixture");
                menuRoot.SetActive(false);
                createdRoots.Add(menuRoot);
                Button demo = CreateButton("Demo", menuRoot.transform);
                Button arena = CreateButton("Arena", menuRoot.transform);
                Button custodian = CreateButton("Custodian", menuRoot.transform);
                Button tavern = CreateButton("Tavern", menuRoot.transform);
                MenuNavigationController controller = menuRoot.AddComponent<MenuNavigationController>();
                SetField(controller, "_startDemoButton", demo);
                SetField(controller, "_startArenaButton", arena);
                SetField(controller, "_startCustodianButton", custodian);
                SetField(controller, "_startTavernButton", tavern);
                SetField(controller, "_audioRouter", router);
                menuRoot.SetActive(true);

                yield return null;

                Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(tavern.gameObject),
                    "The menu selects its tavern button as the initial programmatic baseline.");
                Assert.That(router.PendingEffectRequestCount, Is.Zero,
                    "Initial selection must remain silent because the focus relays bind after the baseline is set.");
                Assert.That(router.UserGestureObserved, Is.False);

                eventSystem.SetSelectedGameObject(demo.gameObject);
                Assert.That(router.PendingEffectRequestCount, Is.EqualTo(1));
                Assert.That(PendingEffectIdAt(router, 0), Is.EqualTo(ApplicationAudioRouter.UiFocusEffectId));

                eventSystem.SetSelectedGameObject(demo.gameObject);
                Assert.That(router.PendingEffectRequestCount, Is.EqualTo(1),
                    "Repeating the current selection must not enqueue a duplicate request.");

                eventSystem.SetSelectedGameObject(null);
                Assert.That(router.PendingEffectRequestCount, Is.EqualTo(1),
                    "Clearing selection is a baseline change only and stays silent.");

                eventSystem.SetSelectedGameObject(tavern.gameObject);
                Assert.That(router.PendingEffectRequestCount, Is.EqualTo(2),
                    "Returning from null to a menu button is a new effective selection change.");
                Assert.That(PendingEffectIdAt(router, 1), Is.EqualTo(ApplicationAudioRouter.UiFocusEffectId));
                Assert.That(router.UserGestureObserved, Is.False,
                    "The test keeps the WebGL gesture gate closed and verifies pending IDs without playback.");
            }
            finally
            {
                if (eventSystem != null)
                    eventSystem.SetSelectedGameObject(previousSelection != null ? previousSelection : null);
                for (int index = 0; index < createdRoots.Count; index++)
                {
                    if (createdRoots[index] != null) Object.DestroyImmediate(createdRoots[index]);
                }
                if (createdEventSystem != null) Object.DestroyImmediate(createdEventSystem);
            }
        }

        private static Button CreateButton(string name, Transform parent)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            return buttonObject.GetComponent<Button>();
        }

        private static void SetField(object target, string fieldName, object value)
        {
            typeof(MenuNavigationController).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
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
