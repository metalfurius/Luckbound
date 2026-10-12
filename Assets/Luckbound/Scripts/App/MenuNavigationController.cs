using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Luckbound.App
{
    /// <summary>Routes the menu to the movement prototype or the combat arena.</summary>
    public sealed class MenuNavigationController : MonoBehaviour
    {
        [SerializeField] private Button _startDemoButton;
        [SerializeField] private Button _startArenaButton;

        private void OnEnable()
        {
            if (_startDemoButton == null || _startArenaButton == null)
            {
                throw new System.InvalidOperationException("MenuNavigationController requires both prototype and combat-arena buttons.");
            }

            _startDemoButton.onClick.AddListener(StartMovementDemo);
            _startArenaButton.onClick.AddListener(StartCombatArena);
        }

        private void Start()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            {
                EventSystem.current.SetSelectedGameObject(_startArenaButton.gameObject);
            }
        }

        private void OnDisable()
        {
            if (_startDemoButton != null)
            {
                _startDemoButton.onClick.RemoveListener(StartMovementDemo);
            }
            if (_startArenaButton != null) _startArenaButton.onClick.RemoveListener(StartCombatArena);
        }

        public void StartMovementDemo()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Expedition", LoadSceneMode.Single);
        }

        public void StartCombatArena()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("CombatArena", LoadSceneMode.Single);
        }
    }
}
