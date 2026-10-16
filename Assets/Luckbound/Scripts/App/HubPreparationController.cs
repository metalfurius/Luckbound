using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Progression;
using Luckbound.Core.Saves;
using UnityEngine;
using UnityEngine.Events;
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
        private SkillCatalog _skillCatalog;
        private List<SkillDefinition> _classSkills = new List<SkillDefinition>(3);
        private int _selectedClassIndex;
        private string _selectedRaceId = ExpeditionRunConfiguration.HumanRaceId;
        private string _statusMessage;
        private string _profileStatusMessage;
        private bool _startingExpedition;
        private bool _purchasingUpgrade;
        private string _upgradeStatusMessage;

        public string SelectedClassId => PlayableClasses[_selectedClassIndex];
        public string SelectedRaceId => _selectedRaceId;
        public IReadOnlyList<string> SelectedSkillIds => _selectedSkillIds.AsReadOnly();

        private void OnEnable()
        {
            ValidateReferences();
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
        }

        private void Start()
        {
            try
            {
                TextAsset skillData = Resources.Load<TextAsset>("Luckbound/Data/Skills");
                if (skillData == null) throw new InvalidOperationException("Skills.json could not be loaded.");
                _skillCatalog = SkillCatalogJsonLoader.Load(skillData.text);
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
                ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create(
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
            _upgradeStatusMessage = string.Empty;
            _upgradePanel.SetActive(true);
            RefreshView();
        }

        public void CloseUpgradePanel()
        {
            if (_upgradePanel == null) return;
            _upgradePanel.SetActive(false);
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
