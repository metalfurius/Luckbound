using UnityEngine;
using UnityEngine.SceneManagement;

namespace Luckbound.App
{
    /// <summary>
    /// Owns the application lifetime and routes a fresh launch to the main menu.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class ApplicationBootstrapper : MonoBehaviour
    {
        private static ApplicationBootstrapper _instance;

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

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
