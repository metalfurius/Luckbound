using UnityEngine;
using UnityEngine.SceneManagement;
using Luckbound.Core.Expeditions;

namespace Luckbound.App
{
    /// <summary>
    /// Owns the application lifetime and routes a fresh launch to the main menu.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class ApplicationBootstrapper : MonoBehaviour
    {
        private static ApplicationBootstrapper _instance;

        public static ApplicationBootstrapper Instance => _instance;

        public ExpeditionRunConfiguration ActiveRun { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (SceneManager.GetActiveScene().name == "Bootstrap")
            {
                SceneManager.LoadScene("Menu", LoadSceneMode.Single);
            }
        }

        public void BeginExpedition(ExpeditionRunConfiguration run)
        {
            ActiveRun = run ?? throw new System.ArgumentNullException(nameof(run));
        }

        public void ClearActiveRun()
        {
            ActiveRun = null;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
