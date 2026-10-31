using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Progression;
using Luckbound.Core.Progression.Contracts;
using Luckbound.Core.Saves;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Luckbound.App
{
    /// <summary>Collects the unlocked class, two equipped skills and race choice in the tavern.</summary>
    public sealed class HubPreparationController : MonoBehaviour
    {
        private static readonly string[] PlayableClasses = { "class.bellator", "class.venator", "class.magus" };
        private static readonly string[] ClassLabels = { "BELLATOR", "VENATOR", "MAGUS" };
        private static readonly string[] OtherRaceIds =
        {
            ExpeditionRunConfiguration.OrciniRaceId,
            ExpeditionRunConfiguration.AlvariRaceId,
            ExpeditionRunConfiguration.DvergarRaceId
        };
        private static readonly string[] OtherRaceLabels = { "ORCINI", "ALVARI", "DVERGAR" };
        private static readonly string[] OtherRaceUnlockLabels =
        {
            "CONTRATO DE COMBATE",
            "CONTRATO DE HISTORIA",
            "CONTRATO DE FABRICACIÓN"
        };
        private static readonly string[] GuildIntroNameKeys =
        {
            "npc.maelis.name",
            "npc.ronan.name",
            "npc.talia.name",
            "npc.bram.name",
            "npc.neris.name"
        };
        private static readonly string[] GuildIntroTextKeys =
        {
            "npc.maelis.intro",
            "npc.ronan.intro",
            "npc.talia.intro",
            "npc.bram.intro",
            "npc.neris.intro"
        };
        private static ApplicationBootstrapper _contractUiAudioStateOwner;
        private static PendingContractUiOperation _pendingContractUiOperation;
        private static readonly HashSet<string> RoutedContractUiConfirmations =
            new HashSet<string>(StringComparer.Ordinal);

        [SerializeField] private Button[] _classButtons;
        [SerializeField] private Button[] _skillButtons;
        [SerializeField] private Button _humanRaceButton;
        [SerializeField] private Button[] _lockedRaceButtons;
        [SerializeField] private Button _startExpeditionButton;
        [SerializeField] private Button _returnToMenuButton;
        [SerializeField] private Text _selectionSummaryText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Button _openUpgradesButton;
        [SerializeField] private GameObject _upgradePanel;
        [SerializeField] private Text _upgradeBalanceText;
        [SerializeField] private Text _upgradeStatusText;
        [SerializeField] private Button _healthUpgradeButton;
        [SerializeField] private Button _energyUpgradeButton;
        [SerializeField] private Button _potionUpgradeButton;
        [SerializeField] private Button _closeUpgradesButton;

        private readonly List<string> _selectedSkillIds = new List<string>(2);
        private UnityAction[] _classButtonHandlers;
        private UnityAction[] _skillButtonHandlers;
        private UnityAction[] _raceButtonHandlers;
        private UnityAction _openUpgradesHandler;
        private UnityAction _closeUpgradesHandler;
        private UnityAction _healthUpgradeHandler;
        private UnityAction _energyUpgradeHandler;
        private UnityAction _potionUpgradeHandler;
        private UnityAction _openContractsHandler;
        private UnityAction _closeContractsHandler;
        private UnityAction _retryContractWriteHandler;
        private UnityAction _contractLanguageHandler;
        private UnityAction[] _contractActionHandlers;
        private UnityAction _openGuildIntroHandler;
        private UnityAction _guildLanguageHandler;
        private UnityAction _guildBackHandler;
        private UnityAction _guildCloseHandler;
        private UnityAction[] _guildCharacterHandlers;
        private SkillCatalog _skillCatalog;
        private List<SkillDefinition> _classSkills = new List<SkillDefinition>(3);
        private readonly List<Button> _contractActionButtons = new List<Button>(4);
        private readonly List<Text> _contractTitleTexts = new List<Text>(4);
        private readonly List<Text> _contractObjectiveTexts = new List<Text>(4);
        private readonly List<Text> _contractRewardTexts = new List<Text>(4);
        private readonly List<Text> _contractProgressTexts = new List<Text>(4);
        private readonly List<Button> _guildCharacterButtons = new List<Button>(5);
        private readonly List<Button> _guildLanguageButtons = new List<Button>(2);
        private Button _openContractsButton;
        private Button _closeContractsButton;
        private Button _retryContractWriteButton;
        private Button _contractLanguageButton;
        private GameObject _contractPanel;
        private Text _contractBoardTitleText;
        private Text _contractActiveLimitText;
        private Text _contractStatusText;
        private Text _contractLanguageStatusText;
        private Button _openGuildIntroButton;
        private Button _guildListLanguageButton;
        private Button _guildConversationLanguageButton;
        private Button _guildBackButton;
        private Button _guildListCloseButton;
        private Button _guildConversationCloseButton;
        private GameObject _guildIntroRoot;
        private GameObject _guildListView;
        private GameObject _guildConversationView;
        private Text _guildListTitleText;
        private Text _guildCharacterNameText;
        private Text _guildIntroText;
        private LocalizationService _localization;
        private GameSettingsSaveData _pendingLanguageSettings;
        private GameObject _selectionBeforeContracts;
        private string _contractFeedback = string.Empty;
        private string _languageFeedback = string.Empty;
        private int _selectedClassIndex;
        private string _selectedRaceId = ExpeditionRunConfiguration.HumanRaceId;
        private string _statusMessage;
        private string _profileStatusMessage;
        private bool _startingExpedition;
        private bool _purchasingUpgrade;
        private bool _contractOperationInProgress;
        private bool _languageWriteInProgress;
        private bool _restoreGuildOpenerFocusOnEnable;
        private int _selectedGuildIntroCharacterIndex = -1;
        private string _upgradeStatusMessage;

        public string SelectedClassId => PlayableClasses[_selectedClassIndex];
        public string SelectedRaceId => _selectedRaceId;
        public IReadOnlyList<string> SelectedSkillIds => _selectedSkillIds.AsReadOnly();

        private void OnEnable()
        {
            ValidateReferences();
            EnsureContractPresentation();
            EnsureGuildIntroPresentation();
            if (_localization != null)
            {
                _localization.LanguageChanged -= OnContractLanguageChanged;
                _localization.LanguageChanged += OnContractLanguageChanged;
            }
            _classButtonHandlers = new UnityAction[_classButtons.Length];
            for (int index = 0; index < _classButtons.Length; index++)
            {
                int capturedIndex = index;
                _classButtonHandlers[index] = () => SelectClass(capturedIndex);
                _classButtons[index].onClick.AddListener(_classButtonHandlers[index]);
            }
            _skillButtonHandlers = new UnityAction[_skillButtons.Length];
            for (int index = 0; index < _skillButtons.Length; index++)
            {
                int capturedIndex = index;
                _skillButtonHandlers[index] = () => ToggleSkill(capturedIndex);
                _skillButtons[index].onClick.AddListener(_skillButtonHandlers[index]);
            }
            _humanRaceButton.onClick.AddListener(SelectHumanRace);
            _raceButtonHandlers = new UnityAction[_lockedRaceButtons.Length];
            for (int index = 0; index < _lockedRaceButtons.Length; index++)
            {
                int capturedIndex = index;
                _raceButtonHandlers[index] = () => SelectRace(OtherRaceIds[capturedIndex]);
                _lockedRaceButtons[index].onClick.AddListener(_raceButtonHandlers[index]);
            }
            _startExpeditionButton.onClick.AddListener(StartExpedition);
            _returnToMenuButton.onClick.AddListener(ReturnToMenu);
            _openUpgradesHandler = OpenUpgradePanel;
            _closeUpgradesHandler = CloseUpgradePanel;
            _healthUpgradeHandler = () => PurchasePermanentUpgrade(PermanentUpgradeKind.MaximumHealth);
            _energyUpgradeHandler = () => PurchasePermanentUpgrade(PermanentUpgradeKind.MaximumEnergy);
            _potionUpgradeHandler = () => PurchasePermanentUpgrade(PermanentUpgradeKind.AdditionalHealthPotion);
            if (_openUpgradesButton != null) _openUpgradesButton.onClick.AddListener(_openUpgradesHandler);
            if (_closeUpgradesButton != null) _closeUpgradesButton.onClick.AddListener(_closeUpgradesHandler);
            if (_healthUpgradeButton != null) _healthUpgradeButton.onClick.AddListener(_healthUpgradeHandler);
            if (_energyUpgradeButton != null) _energyUpgradeButton.onClick.AddListener(_energyUpgradeHandler);
            if (_potionUpgradeButton != null) _potionUpgradeButton.onClick.AddListener(_potionUpgradeHandler);
            _openContractsHandler = OpenContractBoard;
            _closeContractsHandler = CloseContractBoard;
            _retryContractWriteHandler = RetryPendingContractWrite;
            _contractLanguageHandler = ChangeContractLanguage;
            if (_openContractsButton != null) _openContractsButton.onClick.AddListener(_openContractsHandler);
            if (_closeContractsButton != null) _closeContractsButton.onClick.AddListener(_closeContractsHandler);
            if (_retryContractWriteButton != null) _retryContractWriteButton.onClick.AddListener(_retryContractWriteHandler);
            if (_contractLanguageButton != null) _contractLanguageButton.onClick.AddListener(_contractLanguageHandler);
            _contractActionHandlers = new UnityAction[_contractActionButtons.Count];
            for (int index = 0; index < _contractActionButtons.Count; index++)
            {
                string contractId = ContractCatalog.ImplementedContracts[index].Id;
                _contractActionHandlers[index] = () => ActivateContract(contractId);
                _contractActionButtons[index].onClick.AddListener(_contractActionHandlers[index]);
            }
            _openGuildIntroHandler = OpenGuildIntro;
            _guildLanguageHandler = ChangeContractLanguage;
            _guildBackHandler = BackToGuildIntroList;
            _guildCloseHandler = CloseGuildIntro;
            if (_openGuildIntroButton != null) _openGuildIntroButton.onClick.AddListener(_openGuildIntroHandler);
            for (int index = 0; index < _guildLanguageButtons.Count; index++)
                if (_guildLanguageButtons[index] != null) _guildLanguageButtons[index].onClick.AddListener(_guildLanguageHandler);
            if (_guildBackButton != null) _guildBackButton.onClick.AddListener(_guildBackHandler);
            if (_guildListCloseButton != null) _guildListCloseButton.onClick.AddListener(_guildCloseHandler);
            if (_guildConversationCloseButton != null) _guildConversationCloseButton.onClick.AddListener(_guildCloseHandler);
            _guildCharacterHandlers = new UnityAction[_guildCharacterButtons.Count];
            for (int index = 0; index < _guildCharacterButtons.Count; index++)
            {
                int capturedIndex = index;
                _guildCharacterHandlers[index] = () => SelectGuildIntroCharacter(capturedIndex);
                _guildCharacterButtons[index].onClick.AddListener(_guildCharacterHandlers[index]);
            }
            if (_restoreGuildOpenerFocusOnEnable && _openGuildIntroButton != null &&
                _openGuildIntroButton.gameObject.activeInHierarchy && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_openGuildIntroButton.gameObject);
                _restoreGuildOpenerFocusOnEnable = false;
            }
        }

        private void Update()
        {
            if (_guildIntroRoot == null || !_guildIntroRoot.activeSelf) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseGuildIntro();
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            GameObject activeView = _guildConversationView.activeSelf
                ? _guildConversationView
                : _guildListView;
            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(activeView.transform)) return;

            GameObject firstSelectable = _guildConversationView.activeSelf
                ? _guildBackButton.gameObject
                : _guildCharacterButtons[0].gameObject;
            if (firstSelectable.activeInHierarchy && firstSelectable.GetComponent<Selectable>().interactable)
                eventSystem.SetSelectedGameObject(firstSelectable);
        }

        private void Start()
        {
            try
            {
                TextAsset skillData = Resources.Load<TextAsset>("Luckbound/Data/Skills");
                if (skillData == null) throw new InvalidOperationException("Skills.json could not be loaded.");
                _skillCatalog = SkillCatalogJsonLoader.Load(skillData.text);
                InitializeContractLocalization();
                RefreshGuildIntro();
                RefreshProfileStatus();
                SelectClass(0);
                SelectHumanRace();
                if (_upgradePanel != null) _upgradePanel.SetActive(false);
                RefreshView();
            }
            catch (Exception exception)
            {
                _statusMessage = "No se puede preparar la expedición: " + exception.Message;
                _startExpeditionButton.interactable = false;
                RefreshView();
            }
        }

        private void OnDisable()
        {
            if (_classButtons != null && _classButtonHandlers != null)
                for (int index = 0; index < _classButtons.Length && index < _classButtonHandlers.Length; index++)
                    if (_classButtons[index] != null) _classButtons[index].onClick.RemoveListener(_classButtonHandlers[index]);
            if (_skillButtons != null && _skillButtonHandlers != null)
                for (int index = 0; index < _skillButtons.Length && index < _skillButtonHandlers.Length; index++)
                    if (_skillButtons[index] != null) _skillButtons[index].onClick.RemoveListener(_skillButtonHandlers[index]);
            if (_humanRaceButton != null) _humanRaceButton.onClick.RemoveListener(SelectHumanRace);
            if (_lockedRaceButtons != null && _raceButtonHandlers != null)
                for (int index = 0; index < _lockedRaceButtons.Length && index < _raceButtonHandlers.Length; index++)
                    if (_lockedRaceButtons[index] != null) _lockedRaceButtons[index].onClick.RemoveListener(_raceButtonHandlers[index]);
            if (_startExpeditionButton != null) _startExpeditionButton.onClick.RemoveListener(StartExpedition);
            if (_returnToMenuButton != null) _returnToMenuButton.onClick.RemoveListener(ReturnToMenu);
            if (_openUpgradesButton != null && _openUpgradesHandler != null) _openUpgradesButton.onClick.RemoveListener(_openUpgradesHandler);
            if (_closeUpgradesButton != null && _closeUpgradesHandler != null) _closeUpgradesButton.onClick.RemoveListener(_closeUpgradesHandler);
            if (_healthUpgradeButton != null && _healthUpgradeHandler != null) _healthUpgradeButton.onClick.RemoveListener(_healthUpgradeHandler);
            if (_energyUpgradeButton != null && _energyUpgradeHandler != null) _energyUpgradeButton.onClick.RemoveListener(_energyUpgradeHandler);
            if (_potionUpgradeButton != null && _potionUpgradeHandler != null) _potionUpgradeButton.onClick.RemoveListener(_potionUpgradeHandler);
            if (_openContractsButton != null && _openContractsHandler != null) _openContractsButton.onClick.RemoveListener(_openContractsHandler);
            if (_closeContractsButton != null && _closeContractsHandler != null) _closeContractsButton.onClick.RemoveListener(_closeContractsHandler);
            if (_retryContractWriteButton != null && _retryContractWriteHandler != null) _retryContractWriteButton.onClick.RemoveListener(_retryContractWriteHandler);
            if (_contractLanguageButton != null && _contractLanguageHandler != null) _contractLanguageButton.onClick.RemoveListener(_contractLanguageHandler);
            if (_contractActionButtons != null && _contractActionHandlers != null)
                for (int index = 0; index < _contractActionButtons.Count && index < _contractActionHandlers.Length; index++)
                    if (_contractActionButtons[index] != null) _contractActionButtons[index].onClick.RemoveListener(_contractActionHandlers[index]);
            if (_openGuildIntroButton != null && _openGuildIntroHandler != null)
                _openGuildIntroButton.onClick.RemoveListener(_openGuildIntroHandler);
            if (_guildLanguageButtons != null && _guildLanguageHandler != null)
                for (int index = 0; index < _guildLanguageButtons.Count; index++)
                    if (_guildLanguageButtons[index] != null) _guildLanguageButtons[index].onClick.RemoveListener(_guildLanguageHandler);
            if (_guildBackButton != null && _guildBackHandler != null) _guildBackButton.onClick.RemoveListener(_guildBackHandler);
            if (_guildListCloseButton != null && _guildCloseHandler != null)
                _guildListCloseButton.onClick.RemoveListener(_guildCloseHandler);
            if (_guildConversationCloseButton != null && _guildCloseHandler != null)
                _guildConversationCloseButton.onClick.RemoveListener(_guildCloseHandler);
            if (_guildCharacterButtons != null && _guildCharacterHandlers != null)
                for (int index = 0; index < _guildCharacterButtons.Count && index < _guildCharacterHandlers.Length; index++)
                    if (_guildCharacterButtons[index] != null) _guildCharacterButtons[index].onClick.RemoveListener(_guildCharacterHandlers[index]);
            if (_guildIntroRoot != null && _guildIntroRoot.activeSelf)
            {
                _restoreGuildOpenerFocusOnEnable = true;
                _guildIntroRoot.SetActive(false);
                if (EventSystem.current != null && _openGuildIntroButton != null &&
                    _openGuildIntroButton.gameObject.activeInHierarchy)
                    EventSystem.current.SetSelectedGameObject(_openGuildIntroButton.gameObject);
            }
            if (_localization != null) _localization.LanguageChanged -= OnContractLanguageChanged;
        }

        public void SelectClass(int classIndex)
        {
            if (classIndex < 0 || classIndex >= PlayableClasses.Length) throw new ArgumentOutOfRangeException(nameof(classIndex));
            _selectedClassIndex = classIndex;
            if (_skillCatalog != null)
            {
                _classSkills = _skillCatalog.Definitions.Where(skill => skill.ClassId == SelectedClassId).ToList();
                string[] retained = _selectedSkillIds.Where(skillId => _classSkills.Any(skill => skill.Id == skillId)).Take(2).ToArray();
                _selectedSkillIds.Clear();
                _selectedSkillIds.AddRange(retained);
                foreach (SkillDefinition skill in _classSkills)
                {
                    if (_selectedSkillIds.Count == 2) break;
                    if (!_selectedSkillIds.Contains(skill.Id)) _selectedSkillIds.Add(skill.Id);
                }
            }
            _statusMessage = string.Empty;
            RefreshView();
        }

        public void ToggleSkill(int skillIndex)
        {
            if (skillIndex < 0 || skillIndex >= _classSkills.Count) throw new ArgumentOutOfRangeException(nameof(skillIndex));
            string skillId = _classSkills[skillIndex].Id;
            if (_selectedSkillIds.Remove(skillId))
            {
                _statusMessage = "Elige dos habilidades para salir.";
            }
            else if (_selectedSkillIds.Count == 2)
            {
                _statusMessage = "Quita una habilidad antes de elegir otra.";
            }
            else
            {
                _selectedSkillIds.Add(skillId);
                _statusMessage = string.Empty;
            }
            RefreshView();
        }

        public void SelectHumanRace()
        {
            SelectRace(ExpeditionRunConfiguration.HumanRaceId);
        }

        private void SelectRace(string raceId)
        {
            ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
            ProfileSaveData profile = GetProfileForRaceSelection(app);
            if (!ExpeditionRunConfiguration.IsKnownRaceId(raceId) || !IsRaceUnlocked(profile, raceId))
            {
                _statusMessage = "Esa raza sigue bloqueada en tu perfil.";
                RefreshView();
                return;
            }

            _selectedRaceId = raceId;
            _statusMessage = string.Empty;
            RefreshView();
        }

        public async void StartExpedition()
        {
            ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
            if (_startingExpedition) return;
            if (app != null && app.HasPendingExpeditionWrite)
            {
                _startingExpedition = true;
                RefreshView();
                try
                {
                    SaveWriteResult retry = await app.RetryPendingExpeditionWriteAsync();
                    if (!retry.Succeeded)
                    {
                        _statusMessage = "El guardado sigue pendiente: " + retry.Message;
                        return;
                    }
                    if (app.CanContinueExpedition) app.ContinueActiveExpedition();
                    return;
                }
                catch (Exception exception)
                {
                    _statusMessage = "No se pudo reintentar el guardado: " + exception.Message;
                }
                finally
                {
                    _startingExpedition = false;
                    RefreshView();
                }
            }
            if (app != null && app.HasPendingSettlement)
            {
                _startingExpedition = true;
                RefreshView();
                try
                {
                    ExpeditionSettlementCommitResult retry = await app.RetryPendingSettlementAsync();
                    _statusMessage = retry.Persisted
                        ? "La liquidación se guardó. Puedes preparar otra expedición."
                        : "La liquidación sigue pendiente: " + retry.Message;
                }
                catch (Exception exception)
                {
                    _statusMessage = "No se pudo reintentar la liquidación: " + exception.Message;
                }
                finally
                {
                    _startingExpedition = false;
                    RefreshView();
                }
                return;
            }
            if (app != null && app.CanContinueExpedition)
            {
                try { app.ContinueActiveExpedition(); }
                catch (Exception exception)
                {
                    _statusMessage = "No se pudo reanudar: " + exception.Message;
                    RefreshView();
                }
                return;
            }
            if (app == null || !app.CanStartNewExpedition)
            {
                _profileStatusMessage = ProfileStatusMessage(app);
                RefreshView();
                return;
            }
            if (_selectedSkillIds.Count != 2)
            {
                _statusMessage = "Debes elegir exactamente dos habilidades.";
                RefreshView();
                return;
            }

            try
            {
                ProfileSaveData profile = app.Profile;
                if (!IsRaceUnlocked(profile, SelectedRaceId))
                {
                    _statusMessage = "La raza seleccionada ya no está desbloqueada en tu perfil.";
                    RefreshView();
                    return;
                }
                ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.CreateSample(
                    SelectedClassId, SelectedRaceId, _selectedSkillIds,
                    profile.healthUpgradeLevel,
                    profile.energyUpgradeLevel,
                    profile.additionalPotionUpgradeLevel);
                _startingExpedition = true;
                RefreshView();
                SaveWriteResult save = await app.BeginExpeditionAsync(run);
                if (!save.Succeeded)
                {
                    _statusMessage = "No se pudo guardar la expedición: " + save.Message;
                    RefreshView();
                    return;
                }
                Time.timeScale = 1f;
                SceneManager.LoadScene("Expedition", LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                _statusMessage = "No se pudo iniciar: " + exception.Message;
                RefreshView();
            }
            finally
            {
                _startingExpedition = false;
                RefreshView();
            }
        }

        public void ReturnToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Menu", LoadSceneMode.Single);
        }

        public void OpenUpgradePanel()
        {
            if (_upgradePanel == null) return;
            if (_contractPanel != null) _contractPanel.SetActive(false);
            if (_guildIntroRoot != null) _guildIntroRoot.SetActive(false);
            _upgradeStatusMessage = string.Empty;
            _upgradePanel.SetActive(true);
            RefreshView();
        }

        public void CloseUpgradePanel()
        {
            if (_upgradePanel == null) return;
            _upgradePanel.SetActive(false);
        }

        /// <summary>Changes the active language without rebuilding controls or discarding current selections.</summary>
        public void SetLanguage(string languageId)
        {
            if (_languageWriteInProgress) return;
            if (_localization == null) InitializeContractLocalization();
            if (_pendingLanguageSettings != null)
            {
                RetryPendingLanguageWrite();
                return;
            }
            _localization.SetLanguage(languageId);
            PersistLanguage(languageId);
        }

        private void EnsureContractPresentation()
        {
            if (_openContractsButton != null) return;
            Canvas canvas = GameObject.Find("HubPreparationCanvas")?.GetComponent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            _openContractsButton = CreateRuntimeButton("OpenContractsButton", "CONTRATOS", canvas.transform,
                new Vector2(12f, -12f), new Vector2(72f, 18f), 6, "#10111B", "#AA7DC2", topLeft: true);

            _contractPanel = new GameObject("ContractBoardPanel", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            _contractPanel.transform.SetParent(canvas.transform, false);
            RectTransform overlayRect = _contractPanel.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            Image overlayImage = _contractPanel.GetComponent<Image>();
            overlayImage.color = new Color(0.025f, 0.03f, 0.055f, 0.84f);
            overlayImage.raycastTarget = true;
            Button overlayButton = _contractPanel.GetComponent<Button>();
            overlayButton.targetGraphic = overlayImage;
            overlayButton.transition = Selectable.Transition.None;
            overlayButton.navigation = new Navigation { mode = Navigation.Mode.None };

            Image board = CreateRuntimeImage("ContractBoard", _contractPanel.transform, "#171621");
            PlaceRuntimeRect(board.rectTransform, Vector2.zero, new Vector2(452f, 250f));
            _contractBoardTitleText = CreateRuntimeText("ContractBoardTitle", "CONTRACTS", board.transform,
                new Vector2(0f, 108f), new Vector2(200f, 22f), 13, "#F0C15A", FontStyle.Bold);
            _contractLanguageButton = CreateRuntimeButton("ContractLanguageButton", "ENGLISH", board.transform,
                new Vector2(-177f, 108f), new Vector2(70f, 18f), 6, "#E8E3CD", "#32251F");
            _closeContractsButton = CreateRuntimeButton("CloseContractsButton", "×", board.transform,
                new Vector2(200f, 108f), new Vector2(30f, 20f), 11, "#E8E3CD", "#493363");
            _contractActiveLimitText = CreateRuntimeText("ContractActiveLimit", string.Empty, board.transform,
                new Vector2(0f, 86f), new Vector2(410f, 15f), 7, "#AA7DC2", FontStyle.Bold);
            _contractLanguageStatusText = CreateRuntimeText("ContractLanguageStatus", string.Empty, board.transform,
                new Vector2(0f, 69f), new Vector2(410f, 14f), 6, "#BDC6CC", FontStyle.Normal);

            IReadOnlyList<ContractDefinition> contracts = ContractCatalog.ImplementedContracts;
            for (int index = 0; index < contracts.Count; index++)
            {
                ContractDefinition definition = contracts[index];
                float rowY = 44f - index * 44f;
                Image row = CreateRuntimeImage("ContractCard" + definition.Id, board.transform, "#282338");
                PlaceRuntimeRect(row.rectTransform, new Vector2(0f, rowY), new Vector2(434f, 40f));
                _contractTitleTexts.Add(CreateRuntimeText("Contract" + definition.Id + "Title", string.Empty,
                    row.transform, new Vector2(-28f, 13f), new Vector2(280f, 12f), 7, "#F0C15A", FontStyle.Bold));
                _contractObjectiveTexts.Add(CreateRuntimeText("Contract" + definition.Id + "Objective", string.Empty,
                    row.transform, new Vector2(-28f, 1f), new Vector2(280f, 12f), 6, "#E8E3CD", FontStyle.Normal));
                _contractRewardTexts.Add(CreateRuntimeText("Contract" + definition.Id + "Reward", string.Empty,
                    row.transform, new Vector2(-28f, -12f), new Vector2(280f, 12f), 5, "#BDC6CC", FontStyle.Normal));
                _contractProgressTexts.Add(CreateRuntimeText("Contract" + definition.Id + "Progress", string.Empty,
                    row.transform, new Vector2(137f, 0f), new Vector2(50f, 16f), 6, "#E8E3CD", FontStyle.Bold));
                _contractActionButtons.Add(CreateRuntimeButton("Contract" + definition.Id + "ActionButton", "",
                    row.transform, new Vector2(187f, 0f), new Vector2(64f, 24f), 6, "#10111B", "#D68F32"));
            }

            _contractStatusText = CreateRuntimeText("ContractBoardStatus", string.Empty, board.transform,
                new Vector2(-89f, -115f), new Vector2(240f, 15f), 6, "#F0C15A", FontStyle.Normal);
            _retryContractWriteButton = CreateRuntimeButton("RetryPendingContractProgressionButton", "RETRY",
                board.transform, new Vector2(167f, -115f), new Vector2(92f, 19f), 6, "#E8E3CD", "#493363");
            _contractPanel.SetActive(false);
        }

        private void EnsureGuildIntroPresentation()
        {
            if (_openGuildIntroButton != null && _guildIntroRoot != null) return;
            Canvas canvas = GameObject.Find("HubPreparationCanvas")?.GetComponent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            _openGuildIntroButton = CreateRuntimeButton("OpenGuildIntroButton", "GREMIO", canvas.transform,
                new Vector2(92f, -10f), new Vector2(108f, 28f), 21, "#E8E3CD", "#493363", topLeft: true);
            ReserveGuildHeaderSpace(canvas);

            _guildIntroRoot = new GameObject("GuildIntroRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _guildIntroRoot.transform.SetParent(canvas.transform, false);
            RectTransform overlayRect = _guildIntroRoot.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            Image overlay = _guildIntroRoot.GetComponent<Image>();
            overlay.color = new Color(0.025f, 0.03f, 0.055f, 0.88f);
            overlay.raycastTarget = true;

            Image board = CreateRuntimeImage("GuildIntroBoard", _guildIntroRoot.transform, "#171621");
            board.raycastTarget = true;
            PlaceRuntimeRect(board.rectTransform, Vector2.zero, new Vector2(430f, 240f));

            _guildListView = new GameObject("GuildIntroListView", typeof(RectTransform));
            _guildListView.transform.SetParent(board.transform, false);
            PlaceRuntimeRect(_guildListView.GetComponent<RectTransform>(), Vector2.zero, new Vector2(430f, 240f));
            _guildConversationView = new GameObject("GuildIntroConversationView", typeof(RectTransform));
            _guildConversationView.transform.SetParent(board.transform, false);
            PlaceRuntimeRect(_guildConversationView.GetComponent<RectTransform>(), Vector2.zero, new Vector2(430f, 240f));

            _guildListTitleText = CreateRuntimeText("GuildIntroListTitle", string.Empty, _guildListView.transform,
                new Vector2(60f, 99f), new Vector2(210f, 30f), 24, "#F0C15A", FontStyle.Bold);
            _guildListTitleText.alignment = TextAnchor.MiddleCenter;
            _guildListLanguageButton = CreateRuntimeButton("GuildIntroListLanguageButton", "ENGLISH", _guildListView.transform,
                new Vector2(-140f, 99f), new Vector2(140f, 30f), 21, "#E8E3CD", "#32251F");
            _guildListCloseButton = CreateRuntimeButton("GuildIntroListCloseButton", string.Empty, _guildListView.transform,
                new Vector2(0f, -101f), new Vector2(120f, 28f), 21, "#E8E3CD", "#493363");

            float[] rowPositions = { 62f, 30f, -2f, -34f, -66f };
            for (int index = 0; index < GuildIntroNameKeys.Length; index++)
            {
                Button characterButton = CreateRuntimeButton("GuildCharacterButton" + index, string.Empty,
                    _guildListView.transform, new Vector2(0f, rowPositions[index]), new Vector2(390f, 28f),
                    21, "#E8E3CD", "#282338");
                _guildCharacterButtons.Add(characterButton);
            }

            _guildConversationLanguageButton = CreateRuntimeButton("GuildIntroConversationLanguageButton", "ENGLISH",
                _guildConversationView.transform, new Vector2(-140f, 99f), new Vector2(140f, 30f),
                21, "#E8E3CD", "#32251F");
            _guildCharacterNameText = CreateRuntimeText("GuildIntroCharacterName", string.Empty,
                _guildConversationView.transform, new Vector2(0f, 62f), new Vector2(300f, 30f),
                24, "#F0C15A", FontStyle.Bold);
            _guildCharacterNameText.alignment = TextAnchor.MiddleCenter;
            _guildIntroText = CreateRuntimeText("GuildIntroText", string.Empty,
                _guildConversationView.transform, new Vector2(0f, -2f), new Vector2(390f, 120f),
                21, "#E8E3CD", FontStyle.Normal);
            _guildIntroText.alignment = TextAnchor.MiddleCenter;
            _guildIntroText.verticalOverflow = VerticalWrapMode.Truncate;
            _guildBackButton = CreateRuntimeButton("GuildIntroBackButton", string.Empty, _guildConversationView.transform,
                new Vector2(-74f, -101f), new Vector2(130f, 28f), 21, "#E8E3CD", "#32251F");
            _guildConversationCloseButton = CreateRuntimeButton("GuildIntroConversationCloseButton", string.Empty,
                _guildConversationView.transform, new Vector2(74f, -101f), new Vector2(130f, 28f),
                21, "#E8E3CD", "#493363");

            _guildLanguageButtons.Add(_guildListLanguageButton);
            _guildLanguageButtons.Add(_guildConversationLanguageButton);
            ConfigureGuildIntroNavigation();
            _guildConversationView.SetActive(false);
            _guildIntroRoot.SetActive(false);
        }

        private static void ReserveGuildHeaderSpace(Canvas canvas)
        {
            Transform titleTransform = canvas.transform.Find("HubTitle");
            Text title = titleTransform == null ? null : titleTransform.GetComponent<Text>();
            if (title == null) throw new InvalidOperationException("HubTitle is required to reserve the guild button header slot.");
            RectTransform rect = title.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(43f, 111f);
            rect.sizeDelta = new Vector2(150f, 28f);
        }

        private void ConfigureGuildIntroNavigation()
        {
            if (_guildCharacterButtons.Count != GuildIntroNameKeys.Length) return;
            for (int index = 0; index < _guildCharacterButtons.Count; index++)
            {
                Navigation navigation = new Navigation { mode = Navigation.Mode.Explicit };
                navigation.selectOnUp = index == 0 ? _guildListLanguageButton : _guildCharacterButtons[index - 1];
                navigation.selectOnDown = index == _guildCharacterButtons.Count - 1
                    ? _guildListCloseButton
                    : _guildCharacterButtons[index + 1];
                _guildCharacterButtons[index].navigation = navigation;
            }

            Navigation listLanguage = new Navigation { mode = Navigation.Mode.Explicit };
            listLanguage.selectOnDown = _guildCharacterButtons[0];
            _guildListLanguageButton.navigation = listLanguage;
            Navigation listClose = new Navigation { mode = Navigation.Mode.Explicit };
            listClose.selectOnUp = _guildCharacterButtons[_guildCharacterButtons.Count - 1];
            _guildListCloseButton.navigation = listClose;

            Navigation conversationLanguage = new Navigation { mode = Navigation.Mode.Explicit };
            conversationLanguage.selectOnDown = _guildBackButton;
            _guildConversationLanguageButton.navigation = conversationLanguage;
            Navigation back = new Navigation { mode = Navigation.Mode.Explicit };
            back.selectOnUp = _guildConversationLanguageButton;
            back.selectOnRight = _guildConversationCloseButton;
            _guildBackButton.navigation = back;
            Navigation close = new Navigation { mode = Navigation.Mode.Explicit };
            close.selectOnUp = _guildConversationLanguageButton;
            close.selectOnLeft = _guildBackButton;
            _guildConversationCloseButton.navigation = close;
        }

        private void OpenGuildIntro()
        {
            if (_guildIntroRoot == null) return;
            if (_contractPanel != null) _contractPanel.SetActive(false);
            if (_upgradePanel != null) _upgradePanel.SetActive(false);
            _selectedGuildIntroCharacterIndex = -1;
            _guildListView.SetActive(true);
            _guildConversationView.SetActive(false);
            RefreshGuildIntro();
            _guildIntroRoot.SetActive(true);
            if (EventSystem.current != null && _guildCharacterButtons.Count > 0)
                EventSystem.current.SetSelectedGameObject(_guildCharacterButtons[0].gameObject);
        }

        private void SelectGuildIntroCharacter(int index)
        {
            if (_guildIntroRoot == null || !_guildIntroRoot.activeSelf ||
                index < 0 || index >= GuildIntroNameKeys.Length || index >= _guildCharacterButtons.Count)
                return;
            _selectedGuildIntroCharacterIndex = index;
            _guildListView.SetActive(false);
            _guildConversationView.SetActive(true);
            RefreshGuildIntro();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_guildBackButton.gameObject);
        }

        private void BackToGuildIntroList()
        {
            if (_guildIntroRoot == null || !_guildIntroRoot.activeSelf ||
                _selectedGuildIntroCharacterIndex < 0 || _selectedGuildIntroCharacterIndex >= _guildCharacterButtons.Count)
                return;
            int selectedIndex = _selectedGuildIntroCharacterIndex;
            _guildConversationView.SetActive(false);
            _guildListView.SetActive(true);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_guildCharacterButtons[selectedIndex].gameObject);
        }

        private void CloseGuildIntro()
        {
            if (_guildIntroRoot == null) return;
            _guildIntroRoot.SetActive(false);
            _selectedGuildIntroCharacterIndex = -1;
            if (EventSystem.current != null && _openGuildIntroButton != null)
                EventSystem.current.SetSelectedGameObject(_openGuildIntroButton.gameObject);
        }

        private void RefreshGuildIntro()
        {
            if (_openGuildIntroButton == null || _guildIntroRoot == null) return;
            string language = _localization == null ? "es" : _localization.LanguageId;
            string languageKey = language == "es" ? "ui.language.en" : "ui.language.es";
            string languageButtonLabel = _languageWriteInProgress
                ? LocalizedStatus("GUARDANDO", "SAVING")
                : _pendingLanguageSettings != null
                    ? LocalizedStatus("REINTENTAR", "RETRY")
                    : GetText(languageKey);
            SetLabel(_openGuildIntroButton, GetText("ui.guild.title"));
            if (_guildListTitleText != null) _guildListTitleText.text = GetText("ui.guild.title");
            if (_guildListLanguageButton != null) SetLabel(_guildListLanguageButton, languageButtonLabel);
            if (_guildConversationLanguageButton != null) SetLabel(_guildConversationLanguageButton, languageButtonLabel);
            if (_guildListCloseButton != null) SetLabel(_guildListCloseButton, GetText("ui.guild.close"));
            if (_guildConversationCloseButton != null) SetLabel(_guildConversationCloseButton, GetText("ui.guild.close"));
            if (_guildBackButton != null) SetLabel(_guildBackButton, GetText("ui.guild.back"));
            for (int index = 0; index < _guildCharacterButtons.Count && index < GuildIntroNameKeys.Length; index++)
                SetLabel(_guildCharacterButtons[index], GetText(GuildIntroNameKeys[index]));
            if (_selectedGuildIntroCharacterIndex >= 0 && _selectedGuildIntroCharacterIndex < GuildIntroNameKeys.Length)
            {
                if (_guildCharacterNameText != null)
                    _guildCharacterNameText.text = GetText(GuildIntroNameKeys[_selectedGuildIntroCharacterIndex]);
                if (_guildIntroText != null)
                    _guildIntroText.text = GetText(GuildIntroTextKeys[_selectedGuildIntroCharacterIndex]);
            }
            else
            {
                if (_guildCharacterNameText != null) _guildCharacterNameText.text = string.Empty;
                if (_guildIntroText != null) _guildIntroText.text = string.Empty;
            }
        }

        private void InitializeContractLocalization()
        {
            TextAsset localizationData = Resources.Load<TextAsset>("Luckbound/Localization/Initial");
            if (localizationData == null) throw new InvalidOperationException("Initial localization catalog could not be loaded.");
            LocalizedTextCatalog catalog = LocalizationJsonLoader.Load(localizationData.text);
            ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
            string languageId = app != null && app.SaveDataReady ? app.Settings.languageId : catalog.DefaultLanguageId;
            _localization = new LocalizationService(catalog, languageId);
            _localization.LanguageChanged -= OnContractLanguageChanged;
            _localization.LanguageChanged += OnContractLanguageChanged;
            RefreshContractBoard();
            RefreshGuildIntro();
        }

        private void OnContractLanguageChanged(string languageId)
        {
            // Updating labels in place preserves the selected Selectable and all preparation state.
            _contractFeedback = string.Empty;
            _languageFeedback = string.Empty;
            RefreshView();
        }

        private void OpenContractBoard()
        {
            if (_contractPanel == null) return;
            if (_upgradePanel != null) _upgradePanel.SetActive(false);
            if (_guildIntroRoot != null) _guildIntroRoot.SetActive(false);
            _selectionBeforeContracts = EventSystem.current == null
                ? null
                : EventSystem.current.currentSelectedGameObject;
            RefreshContractBoard();
            _contractPanel.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_contractLanguageButton.gameObject);
        }

        private void CloseContractBoard()
        {
            if (_contractPanel == null) return;
            _contractPanel.SetActive(false);
            if (EventSystem.current != null)
            {
                GameObject target = _selectionBeforeContracts != null && _selectionBeforeContracts.activeInHierarchy
                    ? _selectionBeforeContracts
                    : _openContractsButton.gameObject;
                EventSystem.current.SetSelectedGameObject(target);
            }
        }

        private async void ActivateContract(string contractId)
        {
            if (_contractOperationInProgress) return;
            ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
            if (app == null || !app.SaveDataReady)
            {
                _contractFeedback = LocalizedStatus("El perfil no está disponible.", "The profile is not available.");
                RefreshContractBoard();
                return;
            }
            EnsureContractUiAudioStateOwner(app);
            ReconcilePendingContractUiOperation(app.ContractProgression);
            if (app.HasPendingContractProgression)
            {
                _contractFeedback = LocalizedStatus("Reintenta primero el guardado pendiente.", "Retry the pending save first.");
                RefreshContractBoard();
                return;
            }
            if (!CanStartFreshContractOperation(app))
            {
                _contractFeedback = !app.CanWriteProfile
                    ? LocalizedStatus("El perfil no permite guardar cambios.", "The profile cannot be changed.")
                    : LocalizedStatus("No se puede cambiar el perfil mientras hay otro guardado pendiente.",
                        "The profile cannot be changed while another save is pending.");
                RefreshContractBoard();
                return;
            }

            ContractProgressionSnapshot before = app.ContractProgression;
            bool claim = before.IsAccepted(contractId) && before.IsComplete(contractId);
            ContractProgressionCommitOperation operation = claim
                ? ContractProgressionCommitOperation.Claim
                : ContractProgressionCommitOperation.Accept;
            var uiOperation = new PendingContractUiOperation(contractId, operation, before);
            _pendingContractUiOperation = null;
            _contractOperationInProgress = true;
            _contractFeedback = string.Empty;
            RefreshContractBoard();
            try
            {
                ContractProgressionCommitResult result = claim
                    ? await app.ClaimContractRewardAsync(contractId)
                    : await app.AcceptContractAsync(contractId);
                _contractFeedback = FormatContractResult(result);
                if (result != null && result.Pending)
                {
                    if (HasExpectedPendingUiResult(app, result, uiOperation))
                        _pendingContractUiOperation = uiOperation;
                    else
                        _pendingContractUiOperation = null;
                }
                else
                {
                    RouteContractUiConfirmation(app, uiOperation, result);
                }
            }
            catch (Exception exception)
            {
                _contractFeedback = LocalizedStatus("No se pudo guardar el contrato.", "The contract could not be saved.") + " " + exception.Message;
            }
            finally
            {
                _contractOperationInProgress = false;
                RefreshView();
            }
        }

        private async void RetryPendingContractWrite()
        {
            if (_contractOperationInProgress) return;
            ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
            if (app == null || !app.SaveDataReady) return;
            EnsureContractUiAudioStateOwner(app);
            ReconcilePendingContractUiOperation(app.ContractProgression);
            if (!app.CanWriteProfile)
            {
                _contractFeedback = LocalizedStatus("El perfil no permite guardar cambios.", "The profile cannot be changed.");
                RefreshContractBoard();
                return;
            }
            if (!app.HasPendingContractProgression) return;

            PendingContractUiOperation uiOperation = _pendingContractUiOperation;
            _contractOperationInProgress = true;
            RefreshContractBoard();
            try
            {
                ContractProgressionCommitResult result = await app.RetryPendingContractProgressionAsync();
                _contractFeedback = FormatContractResult(result);
                if (result != null && result.Pending)
                {
                    if (!app.HasPendingContractProgression || uiOperation == null || result.Operation != uiOperation.Operation)
                        _pendingContractUiOperation = null;
                }
                else
                {
                    if (uiOperation != null)
                        RouteContractUiConfirmation(app, uiOperation, result);
                    _pendingContractUiOperation = null;
                }
            }
            catch (Exception exception)
            {
                _contractFeedback = LocalizedStatus("El guardado sigue pendiente.", "The save is still pending.") + " " + exception.Message;
            }
            finally
            {
                _contractOperationInProgress = false;
                RefreshView();
            }
        }

        private static void EnsureContractUiAudioStateOwner(ApplicationBootstrapper app)
        {
            if (ReferenceEquals(_contractUiAudioStateOwner, app)) return;
            _contractUiAudioStateOwner = app;
            _pendingContractUiOperation = null;
            RoutedContractUiConfirmations.Clear();
        }

        private static void ReconcilePendingContractUiOperation(ContractProgressionSnapshot snapshot)
        {
            if (_pendingContractUiOperation != null &&
                HasContractUiTransition(_pendingContractUiOperation, snapshot))
                _pendingContractUiOperation = null;
        }

        private static bool HasExpectedPendingUiResult(
            ApplicationBootstrapper app, ContractProgressionCommitResult result,
            PendingContractUiOperation uiOperation)
        {
            if (app == null || !app.HasPendingContractProgression || result == null || uiOperation == null ||
                !result.Pending || result.Persisted || result.Operation != uiOperation.Operation ||
                !ReferenceEquals(result.Snapshot, uiOperation.Before))
                return false;

            switch (uiOperation.Operation)
            {
                case ContractProgressionCommitOperation.Accept:
                    return result.AcceptStatus == ContractAcceptStatus.Accepted;
                case ContractProgressionCommitOperation.Claim:
                    return result.ClaimStatus == ContractClaimStatus.Claimed;
                default:
                    return false;
            }
        }

        private static void RouteContractUiConfirmation(ApplicationBootstrapper app,
            PendingContractUiOperation uiOperation, ContractProgressionCommitResult result)
        {
            if (app == null || !IsNewDurableContractUiAction(uiOperation, result)) return;

            string operationKey = uiOperation.Operation.ToString() + ":" + uiOperation.ContractId;
            if (!RoutedContractUiConfirmations.Add(operationKey)) return;
            app.AudioRouter?.PlayEffect(ApplicationAudioRouter.UiConfirmEffectId);
        }

        private static bool IsNewDurableContractUiAction(
            PendingContractUiOperation uiOperation, ContractProgressionCommitResult result)
        {
            if (uiOperation == null || result == null || result.Pending || !result.Persisted ||
                result.Operation != uiOperation.Operation || result.Snapshot == null ||
                !HasContractUiTransition(uiOperation, result.Snapshot))
                return false;

            switch (uiOperation.Operation)
            {
                case ContractProgressionCommitOperation.Accept:
                    return result.AcceptStatus == ContractAcceptStatus.Accepted &&
                        result.ClaimStatus == null;
                case ContractProgressionCommitOperation.Claim:
                    return result.ClaimStatus == ContractClaimStatus.Claimed &&
                        result.Reward != null && result.AcceptStatus == null;
                default:
                    return false;
            }
        }

        private static bool HasContractUiTransition(
            PendingContractUiOperation uiOperation, ContractProgressionSnapshot after)
        {
            if (uiOperation == null || uiOperation.Before == null || after == null) return false;

            switch (uiOperation.Operation)
            {
                case ContractProgressionCommitOperation.Accept:
                    return !uiOperation.Before.IsAccepted(uiOperation.ContractId) &&
                        after.IsAccepted(uiOperation.ContractId);
                case ContractProgressionCommitOperation.Claim:
                    return uiOperation.Before.IsAccepted(uiOperation.ContractId) &&
                        uiOperation.Before.IsComplete(uiOperation.ContractId) &&
                        !uiOperation.Before.IsClaimed(uiOperation.ContractId) &&
                        after.IsClaimed(uiOperation.ContractId);
                default:
                    return false;
            }
        }

        private sealed class PendingContractUiOperation
        {
            public PendingContractUiOperation(string contractId, ContractProgressionCommitOperation operation,
                ContractProgressionSnapshot before)
            {
                ContractId = contractId;
                Operation = operation;
                Before = before;
            }

            public string ContractId { get; }
            public ContractProgressionCommitOperation Operation { get; }
            public ContractProgressionSnapshot Before { get; }
        }

        private void ChangeContractLanguage()
        {
            if (_pendingLanguageSettings != null)
            {
                RetryPendingLanguageWrite();
                return;
            }
            if (_localization == null) InitializeContractLocalization();
            SetLanguage(_localization.LanguageId == "es" ? "en" : "es");
        }

        private async void PersistLanguage(string languageId)
        {
            if (_languageWriteInProgress) return;
            ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
            if (app == null || !app.SaveDataReady)
            {
                _languageFeedback = LocalizedStatus("Idioma cambiado para esta sesión.", "Language changed for this session.");
                RefreshContractBoard();
                RefreshGuildIntro();
                return;
            }

            GameSettingsSaveData candidate = app.Settings;
            candidate.languageId = languageId;
            _pendingLanguageSettings = candidate;
            _languageWriteInProgress = true;
            RefreshContractBoard();
            RefreshGuildIntro();
            try
            {
                SaveWriteResult result = await app.SaveSettingsAsync(candidate);
                if (result.Succeeded)
                {
                    _pendingLanguageSettings = null;
                    _languageFeedback = LocalizedStatus("Idioma guardado.", "Language saved.");
                }
                else
                {
                    _languageFeedback = LocalizedStatus("Guardado de idioma pendiente. Pulsa el idioma para reintentar.",
                        "Language save pending. Press the language button to retry.");
                }
            }
            catch (Exception exception)
            {
                _languageFeedback = LocalizedStatus("No se pudo guardar el idioma.", "The language could not be saved.") + " " + exception.Message;
            }
            finally
            {
                _languageWriteInProgress = false;
                RefreshContractBoard();
                RefreshGuildIntro();
            }
        }

        private async void RetryPendingLanguageWrite()
        {
            if (_languageWriteInProgress || _pendingLanguageSettings == null) return;
            ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
            if (app == null || !app.SaveDataReady) return;

            _languageWriteInProgress = true;
            RefreshContractBoard();
            RefreshGuildIntro();
            try
            {
                GameSettingsSaveData requestedSettings = _pendingLanguageSettings;
                SaveWriteResult result = await app.RetryPendingSettingsAsync();
                if (result.Succeeded)
                {
                    if (app.Settings.languageId != requestedSettings.languageId)
                    {
                        result = await app.SaveSettingsAsync(requestedSettings);
                    }
                    if (result.Succeeded)
                    {
                        _pendingLanguageSettings = null;
                        _languageFeedback = LocalizedStatus("Idioma guardado.", "Language saved.");
                    }
                    else
                    {
                        _languageFeedback = LocalizedStatus("El mismo guardado de idioma sigue pendiente.",
                            "The same language save is still pending.");
                    }
                }
                else
                {
                    _languageFeedback = LocalizedStatus("El mismo guardado de idioma sigue pendiente.",
                        "The same language save is still pending.");
                }
            }
            catch (Exception exception)
            {
                _languageFeedback = LocalizedStatus("El mismo guardado de idioma sigue pendiente.",
                    "The same language save is still pending.") + " " + exception.Message;
            }
            finally
            {
                _languageWriteInProgress = false;
                RefreshContractBoard();
                RefreshGuildIntro();
            }
        }

        private void RefreshContractBoard()
        {
            if (_contractPanel == null) return;
            string language = _localization == null ? "es" : _localization.LanguageId;
            ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
            ContractProgressionSnapshot snapshot = app == null ? null : app.ContractProgression;
            int activeCount = snapshot == null ? 0 : snapshot.ActiveContractIds.Count;
            bool canRetryPendingContract = CanRetryPendingContractWrite(app);
            bool canStartFreshContractOperation = CanStartFreshContractOperation(app);
            if (_openContractsButton != null) SetLabel(_openContractsButton, GetText("ui.contracts.title"));
            if (_contractBoardTitleText != null) _contractBoardTitleText.text = GetText("ui.contracts.title");
            if (_contractActiveLimitText != null)
                _contractActiveLimitText.text = GetText("ui.contracts.active_limit") + " · " + activeCount + "/" + ContractProgressionRules.MaximumActiveContracts;
            if (_contractLanguageButton != null)
            {
                string languageKey = language == "es" ? "ui.language.en" : "ui.language.es";
                SetLabel(_contractLanguageButton, GetText(languageKey));
                // Keep the selected control focusable during the asynchronous settings save.
                // ChangeContractLanguage already ignores another activation while its exact write is pending.
                _contractLanguageButton.interactable = true;
            }
            if (_contractLanguageStatusText != null) _contractLanguageStatusText.text = _languageFeedback ?? string.Empty;
            if (_contractStatusText != null) _contractStatusText.text = _contractFeedback ?? string.Empty;
            if (_retryContractWriteButton != null)
            {
                _retryContractWriteButton.gameObject.SetActive(canRetryPendingContract);
                _retryContractWriteButton.interactable = canRetryPendingContract && !_contractOperationInProgress;
                SetLabel(_retryContractWriteButton, LocalizedStatus("Reintentar", "Retry"));
            }

            IReadOnlyList<ContractDefinition> definitions = ContractCatalog.ImplementedContracts;
            for (int index = 0; index < definitions.Count && index < _contractActionButtons.Count; index++)
            {
                ContractDefinition definition = definitions[index];
                bool accepted = snapshot != null && snapshot.IsAccepted(definition.Id);
                bool claimed = snapshot != null && snapshot.IsClaimed(definition.Id);
                bool complete = snapshot != null && snapshot.IsComplete(definition.Id);
                int progress = snapshot == null ? 0 : snapshot.GetProgress(definition.Id);
                if (_contractTitleTexts[index] != null)
                    _contractTitleTexts[index].text = definition.Id + " · " + GetText(definition.TitleLocalizationKey);
                if (_contractObjectiveTexts[index] != null) _contractObjectiveTexts[index].text = GetText(definition.ObjectiveLocalizationKey);
                if (_contractRewardTexts[index] != null) _contractRewardTexts[index].text = GetText(ContractRewardLocalizationKey(definition.Id));
                if (_contractProgressTexts[index] != null)
                    _contractProgressTexts[index].text = (claimed ? "✓ " : accepted ? "● " : "○ ") + progress + "/" + definition.RequiredProgress;

                Button action = _contractActionButtons[index];
                if (claimed)
                {
                    SetLabel(action, GetText("ui.contracts.complete"));
                    action.interactable = false;
                }
                else if (accepted && complete)
                {
                    SetLabel(action, GetText("ui.contracts.claim"));
                    action.interactable = !_contractOperationInProgress && canStartFreshContractOperation;
                }
                else if (accepted)
                {
                    SetLabel(action, progress + "/" + definition.RequiredProgress);
                    action.interactable = false;
                }
                else
                {
                    SetLabel(action, GetText("ui.contracts.accept"));
                    action.interactable = !_contractOperationInProgress && canStartFreshContractOperation &&
                        activeCount < ContractProgressionRules.MaximumActiveContracts;
                }
            }
            // ContractProgressionRules also enforces this limit if the profile changes between UI refreshes.
        }

        private static bool CanStartFreshContractOperation(ApplicationBootstrapper app)
        {
            return app != null && app.SaveDataReady && app.CanWriteProfile &&
                !app.HasPendingContractProgression && !app.HasPendingSessionMutation &&
                !app.HasPendingFortuneMutation;
        }

        private static bool CanRetryPendingContractWrite(ApplicationBootstrapper app)
        {
            return app != null && app.SaveDataReady && app.CanWriteProfile && app.HasPendingContractProgression;
        }

        private string FormatContractResult(ContractProgressionCommitResult result)
        {
            if (result == null) return LocalizedStatus("No se recibió resultado del contrato.", "No contract result was received.");
            if (result.Pending) return LocalizedStatus("Guardado pendiente. Reintenta la misma operación.", "Save pending. Retry the same operation.");
            if (result.AcceptStatus == ContractAcceptStatus.Accepted && result.Persisted)
                return LocalizedStatus("Contrato aceptado.", "Contract accepted.");
            if (result.AcceptStatus == ContractAcceptStatus.AlreadyAccepted)
                return LocalizedStatus("El contrato ya estaba aceptado.", "The contract was already accepted.");
            if (result.AcceptStatus == ContractAcceptStatus.RejectedActiveLimit)
                return GetText("ui.contracts.active_limit");
            if (result.AcceptStatus == ContractAcceptStatus.RejectedUnknownContract ||
                result.ClaimStatus == ContractClaimStatus.RejectedUnknownContract)
                return LocalizedStatus("Contrato desconocido.", "Unknown contract.");
            if (result.ClaimStatus == ContractClaimStatus.Claimed && result.Persisted)
                return LocalizedStatus("Recompensa reclamada.", "Reward claimed.");
            if (result.ClaimStatus == ContractClaimStatus.AlreadyClaimed)
                return LocalizedStatus("La recompensa ya estaba reclamada.", "The reward was already claimed.");
            if (result.ClaimStatus == ContractClaimStatus.RejectedNotAccepted)
                return LocalizedStatus("Acepta el contrato antes de reclamarlo.", "Accept the contract before claiming it.");
            if (result.ClaimStatus == ContractClaimStatus.RejectedIncomplete)
                return LocalizedStatus("El objetivo todavía no está completo.", "The objective is not complete yet.");
            if (result.ClaimStatus == ContractClaimStatus.RejectedRenownOverflow)
                return LocalizedStatus("No se puede añadir esa cantidad de Renombre.", "That amount of Renown cannot be added.");
            return LocalizedStatus("No se pudo guardar el contrato.", "The contract could not be saved.");
        }

        private string GetText(string key)
        {
            return _localization == null ? key : _localization.GetText(key);
        }

        private string LocalizedStatus(string spanish, string english)
        {
            return _localization != null && _localization.LanguageId == "en" ? english : spanish;
        }

        private static string ContractRewardLocalizationKey(string contractId)
        {
            return "contract." + contractId.ToLowerInvariant() + ".reward";
        }

        private static Image CreateRuntimeImage(string name, Transform parent, string color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            Color parsed;
            image.color = ColorUtility.TryParseHtmlString(color, out parsed) ? parsed : Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateRuntimeText(string name, string value, Transform parent, Vector2 position,
            Vector2 size, int fontSize, string color, FontStyle style)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            PlaceRuntimeRect(textObject.GetComponent<RectTransform>(), position, size);
            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = ColorUtility.TryParseHtmlString(color, out Color parsed) ? parsed : Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateRuntimeButton(string name, string label, Transform parent, Vector2 position,
            Vector2 size, int fontSize, string textColor, string backgroundColor, bool topLeft = false)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = topLeft ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = topLeft ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = buttonObject.GetComponent<Image>();
            image.color = ColorUtility.TryParseHtmlString(backgroundColor, out Color parsedBackground) ? parsedBackground : Color.white;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.55f, 0.36f, 0.72f, 1f);
            colors.pressedColor = new Color(0.60f, 0.38f, 0.15f, 1f);
            colors.disabledColor = new Color(0.25f, 0.27f, 0.34f, 1f);
            button.colors = colors;
            Text text = CreateRuntimeText(name + "Label", label, buttonObject.transform, Vector2.zero, size,
                fontSize, textColor, FontStyle.Bold);
            RectTransform labelRect = text.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static void PlaceRuntimeRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public async void PurchasePermanentUpgrade(PermanentUpgradeKind kind)
        {
            if (_purchasingUpgrade) return;
            ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
            if (app == null)
            {
                _upgradeStatusMessage = "El perfil no está disponible.";
                RefreshView();
                return;
            }

            _purchasingUpgrade = true;
            RefreshView();
            try
            {
                ProgressionPurchaseCommitResult result;
                if (app.HasPendingPermanentUpgradePurchase)
                {
                    if (app.PendingPermanentUpgradeKind != kind)
                    {
                        _upgradeStatusMessage = "Reintenta primero la compra pendiente.";
                        return;
                    }
                    result = await app.RetryPendingPermanentUpgradePurchaseAsync();
                }
                else
                {
                    result = await app.PurchasePermanentUpgradeAsync(kind, TransactionId.New());
                }

                _upgradeStatusMessage = FormatPurchaseStatus(result);
            }
            catch (Exception exception)
            {
                _upgradeStatusMessage = "No se pudo guardar la mejora: " + exception.Message;
            }
            finally
            {
                _purchasingUpgrade = false;
                RefreshView();
            }
        }

        private void RefreshView()
        {
            ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
            ProfileSaveData profile = GetProfileForRaceSelection(app);
            if (_classButtons != null)
            {
                for (int index = 0; index < _classButtons.Length && index < ClassLabels.Length; index++)
                {
                    SetLabel(_classButtons[index], (index == _selectedClassIndex ? "◆ " : "") + ClassLabels[index]);
                    SetButtonColors(_classButtons[index], index == _selectedClassIndex, "#32251F", "#F0C15A");
                }
            }

            if (_skillButtons != null)
            {
                for (int index = 0; index < _skillButtons.Length; index++)
                {
                    bool available = index < _classSkills.Count;
                    _skillButtons[index].interactable = available;
                    if (available)
                    {
                        SkillDefinition skill = _classSkills[index];
                        bool selected = _selectedSkillIds.Contains(skill.Id);
                        SetLabel(_skillButtons[index], (selected ? "✓ " : "＋ ") + SkillLabel(skill.Id) + "  ·  " + skill.EnergyCost + " EN");
                        SetButtonColors(_skillButtons[index], selected, "#282338", "#AA7DC2");
                    }
                    else
                    {
                        SetLabel(_skillButtons[index], "—");
                    }
                }
            }

            if (_humanRaceButton != null)
            {
                bool unlocked = IsRaceUnlocked(profile, ExpeditionRunConfiguration.HumanRaceId);
                bool selected = _selectedRaceId == ExpeditionRunConfiguration.HumanRaceId;
                _humanRaceButton.interactable = unlocked;
                SetLabel(_humanRaceButton, (selected ? "◆ " : "") + "HUMANO · " +
                    (unlocked ? "DISPONIBLE · INICIAL · 2 FORTUNA" : "BLOQUEADO"));
                SetButtonColors(_humanRaceButton, selected, "#32251F", "#F0C15A");
            }
            if (_lockedRaceButtons != null)
            {
                for (int index = 0; index < _lockedRaceButtons.Length; index++)
                {
                    string raceId = OtherRaceIds[index];
                    bool unlocked = IsRaceUnlocked(profile, raceId);
                    bool selected = _selectedRaceId == raceId;
                    _lockedRaceButtons[index].interactable = unlocked;
                    SetLabel(_lockedRaceButtons[index], unlocked
                        ? (selected ? "◆ " : "") + OtherRaceLabels[index] + " · DESBLOQUEADA · " + OtherRaceUnlockLabels[index]
                        : "BLOQUEADA · " + OtherRaceLabels[index] + " · " + OtherRaceUnlockLabels[index]);
                    SetButtonColors(_lockedRaceButtons[index], selected, "#32251F", "#F0C15A");
                }
            }

            if (_selectionSummaryText != null)
            {
                string selected = string.Join("  ·  ", _selectedSkillIds.Select(SkillLabel));
                _selectionSummaryText.text = "CLASE  " + ClassLabels[_selectedClassIndex] +
                    "     RAZA  " + RaceLabel(_selectedRaceId) + "\nHABILIDADES  " + (selected.Length == 0 ? "elige dos" : selected);
            }
            RefreshUpgradeControls();
            RefreshContractBoard();
            RefreshGuildIntro();
            if (_statusText != null)
            {
                string selectionStatus = string.IsNullOrEmpty(_statusMessage)
                    ? (_selectedSkillIds.Count == 2 ? StartingStatsStatus() : "Elige exactamente dos habilidades (" + _selectedSkillIds.Count + "/2).")
                    : _statusMessage;
                _statusText.text = string.IsNullOrEmpty(_profileStatusMessage) ? selectionStatus : _profileStatusMessage;
            }
            if (_startExpeditionButton != null)
            {
                bool canContinue = app != null && app.CanContinueExpedition;
                bool canRetry = app != null && app.HasPendingExpeditionWrite;
                bool retrySettlement = app != null && app.HasPendingSettlement;
                bool selectedRaceUnlocked = IsRaceUnlocked(profile, _selectedRaceId);
                SetLabel(_startExpeditionButton, retrySettlement ? "REINTENTAR LIQUIDACIÓN" : canRetry ? "REINTENTAR GUARDADO" : canContinue ? "CONTINUAR EXPEDICIÓN" : "PARTIR");
                _startExpeditionButton.interactable = !_startingExpedition && (retrySettlement || canRetry || canContinue ||
                    (_selectedSkillIds.Count == 2 && selectedRaceUnlocked && app != null && app.CanStartNewExpedition));
            }
        }

        private void RefreshUpgradeControls()
        {
            ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
            if (app == null) return;
            ProfileSaveData profile = app.Profile;
            int renown = profile.availableRenown;
            bool pending = app.HasPendingPermanentUpgradePurchase;
            if (_upgradeBalanceText != null) _upgradeBalanceText.text = "RENOMBRE DISPONIBLE · " + renown;
            if (_upgradeStatusText != null) _upgradeStatusText.text = _upgradeStatusMessage ?? string.Empty;
            SetUpgradeButton(_healthUpgradeButton, app, PermanentUpgradeKind.MaximumHealth,
                profile.healthUpgradeLevel, PermanentUpgradeSnapshot.MaximumHealthUpgradeLevel,
                renown, "SALUD MÁXIMA · +5 %", pending);
            SetUpgradeButton(_energyUpgradeButton, app, PermanentUpgradeKind.MaximumEnergy,
                profile.energyUpgradeLevel, PermanentUpgradeSnapshot.MaximumEnergyUpgradeLevel,
                renown, "ENERGÍA MÁXIMA · +5 %", pending);
            SetUpgradeButton(_potionUpgradeButton, app, PermanentUpgradeKind.AdditionalHealthPotion,
                profile.additionalPotionUpgradeLevel, PermanentUpgradeSnapshot.MaximumPotionUpgradeLevel,
                renown, "POCIÓN INICIAL · +1", pending);
        }

        private void SetUpgradeButton(Button button, ApplicationBootstrapper app, PermanentUpgradeKind kind,
            int level, int maximumLevel, int renown, string label, bool pending)
        {
            if (button == null) return;
            bool retry = pending && app.PendingPermanentUpgradeKind == kind;
            int cost = PermanentUpgradeRules.GetNextCost(kind, level);
            string detail = level >= maximumLevel ? "COMPLETA" : cost + " RENOMBRE";
            SetLabel(button, (retry ? "REINTENTAR · " : "") + label + " · " + level + "/" + maximumLevel + " · " + detail);
            button.interactable = !_purchasingUpgrade && (retry || (!pending && app.CanStartNewExpedition &&
                level < maximumLevel && renown >= cost));
        }

        private string StartingStatsStatus()
        {
            ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
            ProfileSaveData profile = app == null ? ProfileSaveData.CreateNewProfile() : app.Profile;
            int health = PermanentUpgradeRules.ApplyFivePercentPerLevel(ExpeditionRunConfiguration.InitialHealth,
                profile.healthUpgradeLevel);
            int energy = PermanentUpgradeRules.ApplyFivePercentPerLevel(ExpeditionRunConfiguration.InitialEnergy,
                profile.energyUpgradeLevel);
            int potions = ExpeditionRunConfiguration.InitialHealthPotions + profile.additionalPotionUpgradeLevel;
            return "Preparación lista · " + RaceLabel(_selectedRaceId) + " · " + health + " SALUD · " +
                energy + " ENERGÍA · " + potions + " pociones";
        }

        private static ProfileSaveData GetProfileForRaceSelection(ApplicationBootstrapper app)
        {
            if (app == null) return ProfileSaveData.CreateNewProfile();
            return app.SaveDataReady ? app.Profile : null;
        }

        private static bool IsRaceUnlocked(ProfileSaveData profile, string raceId)
        {
            return profile != null && profile.unlockedRaceIds != null &&
                profile.unlockedRaceIds.Contains(raceId, StringComparer.Ordinal);
        }

        private static string RaceLabel(string raceId)
        {
            if (raceId == ExpeditionRunConfiguration.OrciniRaceId) return "ORCINI";
            if (raceId == ExpeditionRunConfiguration.AlvariRaceId) return "ALVARI";
            if (raceId == ExpeditionRunConfiguration.DvergarRaceId) return "DVERGAR";
            return "HUMANO";
        }

        private static string FormatPurchaseStatus(ProgressionPurchaseCommitResult result)
        {
            if (result == null) return "No se recibió resultado de compra.";
            if (result.Pending) return "Guardado pendiente. Pulsa la mejora marcada para reintentar.";
            if (result.Purchase == null) return result.Message;
            if (result.Purchase.Status == PermanentUpgradePurchaseStatus.AlreadyProcessed && result.Persisted)
                return "Esta compra ya estaba guardada; no se volvió a cobrar.";
            if (result.Purchase.Status == PermanentUpgradePurchaseStatus.Purchased && result.Persisted)
                return "Mejora guardada. Renombre disponible: " + result.Purchase.Snapshot.AvailableRenown + ".";
            if (result.Purchase.Status == PermanentUpgradePurchaseStatus.RejectedInsufficientRenown)
                return "Renombre insuficiente. Coste: " + result.Purchase.Cost + ".";
            if (result.Purchase.Status == PermanentUpgradePurchaseStatus.RejectedMaximumLevel)
                return "Esta mejora ya está completa.";
            if (result.Purchase.Status == PermanentUpgradePurchaseStatus.RejectedNotInTavern)
                return "Solo puedes comprar mejoras en la taberna.";
            return result.Message;
        }

        private void RefreshProfileStatus()
        {
            ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
            if (app == null) return;
            if (app.ProfileLoadStatus == SaveLoadStatus.RecoveredBackup || !app.CanStartNewExpedition)
                _profileStatusMessage = ProfileStatusMessage(app);
        }

        private static string ProfileStatusMessage(ApplicationBootstrapper app)
        {
            if (app == null || !app.SaveDataReady) return "El perfil todavía no está disponible. Vuelve a intentarlo en unos segundos.";
            if (app.HasPendingSettlement) return "Hay una liquidación pendiente de guardar. Reinténtala o exporta el guardado antes de iniciar otra expedición.";
            if (app.HasPendingExpeditionWrite) return "La expedición no se ha guardado. Reintenta la escritura antes de continuar.";
            if (app.CanContinueExpedition) return "Hay una expedición guardada. Continúa para volver al punto de entrada de la sala actual.";
            if (app.HasActiveExpedition) return "La expedición guardada necesita revisión y se conserva: " + app.ActiveExpeditionLoadError;
            if (app.ProfileLoadStatus == SaveLoadStatus.Incompatible) return "El perfil pertenece a una versión más nueva. Se conserva sin cambios y no se iniciará una partida nueva.";
            if (app.ProfileLoadStatus == SaveLoadStatus.RecoveredBackup) return "El perfil principal estaba dañado; se recuperó la copia de respaldo. Revisa el Renombre y vuelve a guardar cuando puedas.";
            if (app.ProfileLoadStatus == SaveLoadStatus.Corrupted) return "El perfil y su respaldo no se pudieron leer. Se conservaron y no se sobrescribirán.";
            return "El perfil no está disponible; no se iniciará una expedición.";
        }

        private void ValidateReferences()
        {
            if (_classButtons == null || _classButtons.Length != 3 || _classButtons.Any(button => button == null) ||
                _skillButtons == null || _skillButtons.Length != 3 || _skillButtons.Any(button => button == null) ||
                _humanRaceButton == null || _lockedRaceButtons == null || _lockedRaceButtons.Length != 3 ||
                _lockedRaceButtons.Any(button => button == null) || _startExpeditionButton == null ||
                _returnToMenuButton == null || _selectionSummaryText == null || _statusText == null)
                throw new InvalidOperationException("HubPreparationController requires all class, skill, race, status, and navigation controls.");
        }

        private static void SetLabel(Button button, string value)
        {
            Text label = button == null ? null : button.GetComponentInChildren<Text>(true);
            if (label != null) label.text = value;
        }

        private static void SetButtonColors(Button button, bool selected, string baseColor, string accentColor)
        {
            if (button == null) return;
            Color selectedColor;
            Color idleColor;
            ColorUtility.TryParseHtmlString(selected ? accentColor : baseColor, out selectedColor);
            ColorUtility.TryParseHtmlString(selected ? baseColor : accentColor, out idleColor);
            ColorBlock colors = button.colors;
            colors.normalColor = selected ? selectedColor : idleColor;
            colors.highlightedColor = selected ? selectedColor : idleColor * 1.2f;
            colors.pressedColor = selected ? selectedColor * 0.85f : idleColor * 0.85f;
            button.colors = colors;
        }

        private static string SkillLabel(string skillId)
        {
            string value = skillId ?? string.Empty;
            int separator = value.LastIndexOf('.');
            if (separator >= 0) value = value.Substring(separator + 1);
            return value.Replace('_', ' ').ToUpperInvariant();
        }
    }
}
