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
        [SerializeField] private Button _startCustodianButton;
        [SerializeField] private Button _startTavernButton;

        private ApplicationAudioRouter _audioRouter;
        private MenuNavigationFocusAudioRelay[] _focusAudioRelays;
        private GameObject _lastSelectedMenuButton;
        private bool _focusAudioInitialized;

        private void OnEnable()
        {
            if (_startDemoButton == null || _startArenaButton == null || _startCustodianButton == null || _startTavernButton == null)
            {
                throw new System.InvalidOperationException("MenuNavigationController requires demo, arena, Custodian, and tavern buttons.");
            }

            _startDemoButton.onClick.AddListener(StartMovementDemo);
            _startArenaButton.onClick.AddListener(StartCombatArena);
            _startCustodianButton.onClick.AddListener(StartCustodianArena);
            _startTavernButton.onClick.AddListener(StartTavern);

            if (_focusAudioInitialized) BindFocusAudioRelays();
        }

        private void Start()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject == null)
            {
                eventSystem.SetSelectedGameObject(_startTavernButton.gameObject);
            }

            _audioRouter = ResolveAudioRouter();
            BindFocusAudioRelays();
            _focusAudioInitialized = true;
        }

        private void OnDisable()
        {
            if (_startDemoButton != null)
            {
                _startDemoButton.onClick.RemoveListener(StartMovementDemo);
            }
            if (_startArenaButton != null) _startArenaButton.onClick.RemoveListener(StartCombatArena);
            if (_startCustodianButton != null) _startCustodianButton.onClick.RemoveListener(StartCustodianArena);
            if (_startTavernButton != null) _startTavernButton.onClick.RemoveListener(StartTavern);

            UnbindFocusAudioRelays();
        }

        internal void HandleMenuButtonSelected(GameObject selectedButton)
        {
            EventSystem eventSystem = EventSystem.current;
            if (selectedButton == null || eventSystem == null ||
                eventSystem.currentSelectedGameObject != selectedButton ||
                ReferenceEquals(_lastSelectedMenuButton, selectedButton))
            {
                return;
            }

            _lastSelectedMenuButton = selectedButton;
            if (_audioRouter == null) _audioRouter = ResolveAudioRouter();
            _audioRouter?.PlayEffect(ApplicationAudioRouter.UiFocusEffectId);
        }

        internal void HandleMenuButtonDeselected(GameObject deselectedButton)
        {
            if (ReferenceEquals(_lastSelectedMenuButton, deselectedButton))
                _lastSelectedMenuButton = null;
        }

        private void BindFocusAudioRelays()
        {
            EventSystem eventSystem = EventSystem.current;
            _lastSelectedMenuButton = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
            _focusAudioRelays = new[]
            {
                BindFocusAudioRelay(_startDemoButton),
                BindFocusAudioRelay(_startArenaButton),
                BindFocusAudioRelay(_startCustodianButton),
                BindFocusAudioRelay(_startTavernButton)
            };
        }

        private MenuNavigationFocusAudioRelay BindFocusAudioRelay(Button button)
        {
            MenuNavigationFocusAudioRelay relay = button.GetComponent<MenuNavigationFocusAudioRelay>();
            if (relay == null) relay = button.gameObject.AddComponent<MenuNavigationFocusAudioRelay>();
            relay.Bind(this);
            return relay;
        }

        private void UnbindFocusAudioRelays()
        {
            if (_focusAudioRelays != null)
            {
                for (int index = 0; index < _focusAudioRelays.Length; index++)
                {
                    if (_focusAudioRelays[index] != null) _focusAudioRelays[index].Unbind(this);
                }
            }

            _focusAudioRelays = null;
            _lastSelectedMenuButton = null;
        }

        private ApplicationAudioRouter ResolveAudioRouter()
        {
            if (_audioRouter != null) return _audioRouter;
            ApplicationBootstrapper application = ApplicationBootstrapper.Instance;
            return application == null ? null : application.AudioRouter;
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

        public void StartCustodianArena()
        {
            Time.timeScale = 1f;
            if (ApplicationBootstrapper.Instance != null) ApplicationBootstrapper.Instance.ClearActiveRun();
            SceneManager.LoadScene("CustodianArena", LoadSceneMode.Single);
        }

        public void StartTavern()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Hub", LoadSceneMode.Single);
        }
    }
}
