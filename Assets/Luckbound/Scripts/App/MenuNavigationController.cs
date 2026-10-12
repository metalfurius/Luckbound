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
        [SerializeField] private Button _startTavernButton;

        private void OnEnable()
        {
            if (_startDemoButton == null || _startArenaButton == null || _startTavernButton == null)
            {
                throw new System.InvalidOperationException("MenuNavigationController requires demo, arena, and tavern buttons.");
            }

            _startDemoButton.onClick.AddListener(StartMovementDemo);
            _startArenaButton.onClick.AddListener(StartCombatArena);
            _startTavernButton.onClick.AddListener(StartTavern);
        }

        private void Start()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            {
                EventSystem.current.SetSelectedGameObject(_startTavernButton.gameObject);
            }
        }

        private void OnDisable()
        {
            if (_startDemoButton != null)
            {
                _startDemoButton.onClick.RemoveListener(StartMovementDemo);
            }
            if (_startArenaButton != null) _startArenaButton.onClick.RemoveListener(StartCombatArena);
            if (_startTavernButton != null) _startTavernButton.onClick.RemoveListener(StartTavern);
        }

        public void StartMovementDemo()
        {
            Time.timeScale = 1f;
            if (ApplicationBootstrapper.Instance != null) ApplicationBootstrapper.Instance.ClearActiveRun();
            SceneManager.LoadScene("Expedition", LoadSceneMode.Single);
        }

        public void StartCombatArena()
        {
            Time.timeScale = 1f;
            if (ApplicationBootstrapper.Instance != null) ApplicationBootstrapper.Instance.ClearActiveRun();
            SceneManager.LoadScene("CombatArena", LoadSceneMode.Single);
        }

        public void StartTavern()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Hub", LoadSceneMode.Single);
        }
    }
}
