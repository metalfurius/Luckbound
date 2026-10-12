using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
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

        [SerializeField] private Button[] _classButtons;
        [SerializeField] private Button[] _skillButtons;
        [SerializeField] private Button _humanRaceButton;
        [SerializeField] private Button[] _lockedRaceButtons;
        [SerializeField] private Button _startExpeditionButton;
        [SerializeField] private Button _returnToMenuButton;
        [SerializeField] private Text _selectionSummaryText;
        [SerializeField] private Text _statusText;

        private readonly List<string> _selectedSkillIds = new List<string>(2);
        private UnityAction[] _classButtonHandlers;
        private UnityAction[] _skillButtonHandlers;
        private SkillCatalog _skillCatalog;
        private List<SkillDefinition> _classSkills = new List<SkillDefinition>(3);
        private int _selectedClassIndex;
        private string _statusMessage;
        private string _profileStatusMessage;
        private bool _startingExpedition;

        public string SelectedClassId => PlayableClasses[_selectedClassIndex];
        public string SelectedRaceId => ExpeditionRunConfiguration.HumanRaceId;
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
            _startExpeditionButton.onClick.AddListener(StartExpedition);
            _returnToMenuButton.onClick.AddListener(ReturnToMenu);
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
            if (_startExpeditionButton != null) _startExpeditionButton.onClick.RemoveListener(StartExpedition);
            if (_returnToMenuButton != null) _returnToMenuButton.onClick.RemoveListener(ReturnToMenu);
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
                ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create(
                    SelectedClassId, SelectedRaceId, _selectedSkillIds);
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

        private void RefreshView()
        {
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
                _humanRaceButton.interactable = true;
                SetLabel(_humanRaceButton, "◆ HUMANO · DISPONIBLE · 2 FORTUNA");
            }
            if (_lockedRaceButtons != null)
            {
                string[] labels = { "ORCINI · CONTRATO DE COMBATE", "ALVARI · CONTRATO DE HISTORIA", "DVERGAR · CONTRATO DE FABRICACIÓN" };
                for (int index = 0; index < _lockedRaceButtons.Length; index++)
                {
                    _lockedRaceButtons[index].interactable = false;
                SetLabel(_lockedRaceButtons[index], index < labels.Length ? "BLOQUEADA · " + labels[index] : "BLOQUEADA");
                }
            }

            if (_selectionSummaryText != null)
            {
                string selected = string.Join("  ·  ", _selectedSkillIds.Select(SkillLabel));
                _selectionSummaryText.text = "CLASE  " + ClassLabels[_selectedClassIndex] +
                    "     RAZA  HUMANO\nHABILIDADES  " + (selected.Length == 0 ? "elige dos" : selected);
            }
            if (_statusText != null)
            {
                string selectionStatus = string.IsNullOrEmpty(_statusMessage)
                    ? (_selectedSkillIds.Count == 2 ? "Preparación lista · 100 SALUD · 50 ENERGÍA · 2 pociones" : "Elige exactamente dos habilidades (" + _selectedSkillIds.Count + "/2).")
                    : _statusMessage;
                _statusText.text = string.IsNullOrEmpty(_profileStatusMessage) ? selectionStatus : _profileStatusMessage;
            }
            if (_startExpeditionButton != null)
            {
                ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
                bool canContinue = app != null && app.CanContinueExpedition;
                bool canRetry = app != null && app.HasPendingExpeditionWrite;
                bool retrySettlement = app != null && app.HasPendingSettlement;
                SetLabel(_startExpeditionButton, retrySettlement ? "REINTENTAR LIQUIDACIÓN" : canRetry ? "REINTENTAR GUARDADO" : canContinue ? "CONTINUAR EXPEDICIÓN" : "PARTIR");
                _startExpeditionButton.interactable = !_startingExpedition && (retrySettlement || canRetry || canContinue ||
                    (_selectedSkillIds.Count == 2 && app != null && app.CanStartNewExpedition));
            }
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
