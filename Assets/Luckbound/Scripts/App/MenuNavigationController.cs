using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Luckbound.App
{
    /// <summary>Routes the prototype menu into the current playable movement demo.</summary>
    public sealed class MenuNavigationController : MonoBehaviour
    {
        [SerializeField] private Button _startDemoButton;

        private void OnEnable()
        {
            if (_startDemoButton == null)
            {
                throw new System.InvalidOperationException("MenuNavigationController requires a start-demo button.");
            }

            _startDemoButton.onClick.AddListener(StartMovementDemo);
        }

        private void Start()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            {
                EventSystem.current.SetSelectedGameObject(_startDemoButton.gameObject);
            }
        }

        private void OnDisable()
        {
            if (_startDemoButton != null)
            {
                _startDemoButton.onClick.RemoveListener(StartMovementDemo);
            }
        }

        public void StartMovementDemo()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Expedition", LoadSceneMode.Single);
        }
    }
}
