using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class StartupFlowTests
    {
        [UnityTest]
        public IEnumerator BootstrapLoadsTheVisibleMenu()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);

            for (int frame = 0; frame < 10 && SceneManager.GetActiveScene().name != "Menu"; frame++)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Menu"));
            Assert.That(GameObject.Find("LuckboundTitle"), Is.Not.Null);
        }
    }
}
