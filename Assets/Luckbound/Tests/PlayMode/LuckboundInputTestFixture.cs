using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Luckbound.Tests.PlayMode
{
    /// <summary>
    /// Isolates scene UI action lifetimes from InputTestFixture's global runtime swap.
    /// The module remains enabled during a test and is disabled before either runtime reset.
    /// </summary>
    public abstract class LuckboundInputTestFixture : InputTestFixture
    {
        [SetUp]
        public override void Setup()
        {
            DisableSceneUiInputModules();
            base.Setup();
        }

        [TearDown]
        public override void TearDown()
        {
            DisableSceneUiInputModules();
            base.TearDown();
        }

        private static void DisableSceneUiInputModules()
        {
            InputSystemUIInputModule[] modules = UnityEngine.Object.FindObjectsByType<InputSystemUIInputModule>();
            foreach (InputSystemUIInputModule module in modules)
            {
                if (module != null && module.enabled)
                    module.enabled = false;
            }
        }
    }
}
