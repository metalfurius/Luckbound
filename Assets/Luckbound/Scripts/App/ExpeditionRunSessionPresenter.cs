using System;
using System.Linq;
using System.Threading.Tasks;
using Luckbound.Core.Crafting;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Luckbound.App
{
    /// <summary>Shows the verified run configuration until room gameplay is connected by later tasks.</summary>
    public sealed class ExpeditionRunSessionPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject _overlayRoot;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _summaryText;
        [SerializeField] private Button _returnToTavernButton;
        [SerializeField] private Button _craftHealthPotionButton;
        [SerializeField] private Text _craftStatusText;

        private bool _crafting;

        public TransactionId LastCraftTransactionId { get; private set; }
        public bool IsCrafting => _crafting;
        public string CraftStatusText => _craftStatusText == null ? string.Empty : _craftStatusText.text;

        private void OnEnable()
        {
            if (_returnToTavernButton != null) _returnToTavernButton.onClick.AddListener(ReturnToTavern);
            if (_craftHealthPotionButton != null) _craftHealthPotionButton.onClick.AddListener(OnCraftHealthPotionClicked);
        }

        private void Start()
        {
            if (_overlayRoot == null || _titleText == null || _summaryText == null || _returnToTavernButton == null ||
                _craftHealthPotionButton == null || _craftStatusText == null)
                throw new InvalidOperationException("ExpeditionRunSessionPresenter is missing a serialized UI reference.");

            ExpeditionRunConfiguration run = ApplicationBootstrapper.Instance == null
                ? null
                : ApplicationBootstrapper.Instance.ActiveRun;
            if (run == null)
            {
                _overlayRoot.SetActive(false);
                return;
            }

            _craftHealthPotionButton.interactable = true;
            _craftStatusText.text = string.Empty;
            RefreshRunSummary(run, ApplicationBootstrapper.Instance.ActiveExpedition);
            _overlayRoot.SetActive(true);
        }

        private void OnDisable()
        {
            if (_returnToTavernButton != null) _returnToTavernButton.onClick.RemoveListener(ReturnToTavern);
            if (_craftHealthPotionButton != null) _craftHealthPotionButton.onClick.RemoveListener(OnCraftHealthPotionClicked);
        }

        public async Task<CraftingCommitResult> CraftHealthPotionAsync()
        {
            if (_crafting) return null;
            _crafting = true;
            _craftHealthPotionButton.interactable = false;
            _craftStatusText.text = "GUARDANDO FABRICACIÓN…";
            try
            {
                ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
                if (app == null)
                {
                    _craftStatusText.text = "NO SE PUDO FABRICAR · la expedición no está disponible.";
                    return null;
                }

                if (app.HasPendingExpeditionWrite && !app.HasPendingCraftingWrite)
                {
                    SaveWriteResult pendingSave = await app.RetryPendingExpeditionWriteAsync();
                    _craftStatusText.text = pendingSave.Succeeded
                        ? "GUARDADO ANTERIOR CONFIRMADO · pulsa de nuevo para fabricar."
                        : "GUARDADO PENDIENTE · " + pendingSave.Message;
                    return null;
                }

                CraftingCommitResult result;
                if (app.HasPendingCraftingWrite)
                {
                    result = await app.RetryPendingCraftingAsync();
                }
                else
                {
                    LastCraftTransactionId = TransactionId.New();
                    result = await app.CraftActiveExpeditionAsync(
                        "recipe.consumable.health_potion", LastCraftTransactionId);
                }

                if (result.Outcome != null && result.Outcome.TransactionId.IsValid)
                    LastCraftTransactionId = result.Outcome.TransactionId;
                if (result.Persisted && result.Outcome != null)
                {
                    _craftStatusText.text = FormatCraftingOutcome(result.Outcome);
                    if (app.ActiveRun != null) RefreshRunSummary(app.ActiveRun, app.ActiveExpedition);
                }
                else if (result.Pending)
                {
                    _craftStatusText.text = "GUARDADO PENDIENTE · pulsa de nuevo para reintentar esta operación.";
                }
                else
                {
                    _craftStatusText.text = "NO SE PUDO FABRICAR · " + result.Message;
                }
                return result;
            }
            catch (Exception exception)
            {
                _craftStatusText.text = "NO SE PUDO FABRICAR · " + exception.Message;
                return null;
            }
            finally
            {
                _crafting = false;
                if (_craftHealthPotionButton != null) _craftHealthPotionButton.interactable = true;
            }
        }

        private void OnCraftHealthPotionClicked()
        {
            if (!_crafting) _ = CraftHealthPotionAsync();
        }

        public void ReturnToTavern()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Hub", LoadSceneMode.Single);
        }

        private static string ClassLabel(string classId)
        {
            switch (classId)
            {
                case "class.bellator": return "BELLATOR";
                case "class.venator": return "VENATOR";
                case "class.magus": return "MAGUS";
                default: return classId;
            }
        }

        private static string SkillLabel(string skillId)
        {
            int separator = skillId == null ? -1 : skillId.LastIndexOf('.');
            return (separator >= 0 ? skillId.Substring(separator + 1) : skillId).Replace('_', ' ').ToUpperInvariant();
        }

        public static string FormatMaterialBagSummary(ExpeditionInventory inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            return "BOLSA · MADERA " + inventory.GetMaterialCount(MaterialKind.ShadowWood) +
                " · MINERAL " + inventory.GetMaterialCount(MaterialKind.Ore) +
                " · HIERBA " + inventory.GetMaterialCount(MaterialKind.UmbraHerb) +
                "\nPOLVO " + inventory.GetMaterialCount(MaterialKind.ArcaneDust) +
                " · RESINA " + inventory.GetMaterialCount(MaterialKind.DarkResin) +
                " · ASC. VITAL " + inventory.GetMaterialCount(MaterialKind.VitalEmber);
        }

        private void RefreshRunSummary(ExpeditionRunConfiguration run, ExpeditionSaveData saved)
        {
            _titleText.text = "EXPEDICIÓN PREPARADA · " + ClassLabel(run.ClassId);
            InventoryItemStack quickConsumable = run.Inventory.QuickConsumable;
            int currentHealth = saved == null ? run.StartingHealth : saved.currentHealth;
            int currentEnergy = saved == null ? run.StartingEnergy : saved.currentEnergy;
            int currentFortuna = saved == null ? run.StartingFortunaCharges : saved.fortunaCharges;
            _summaryText.text = "RAZA · HUMANO\nHABILIDADES · " + string.Join("  ·  ", run.SkillIds.Select(SkillLabel)) +
                "\nARMA · " + (run.Inventory.Weapon?.ItemId ?? "sin equipar") +
                "\nARMADURA · " + (run.Inventory.Armor?.ItemId ?? "sin equipar") +
                "\nPOCIONES · " + (quickConsumable == null ? 0 : quickConsumable.Quantity) +
                "\nSALUD / ENERGÍA · " + currentHealth + " / " + currentEnergy +
                "\nFORTUNA · " + currentFortuna +
                "\n" + FormatMaterialBagSummary(run.Inventory) +
                "\nRUTA · " + run.Route.Nodes.Count + " nodos · semilla " + run.Seed.ToString("X8") +
                (saved == null ? string.Empty : "\nSALA · " + saved.currentNodeId + " · " + saved.completedRoomIds.Length + " completadas" +
                    (saved.completedRoomIds.Contains(saved.currentNodeId) ? " · resultado guardado" : " · checkpoint de entrada guardado")) +
                "\nID · " + run.RunId;
        }

        private static string FormatCraftingOutcome(CraftingOutcome outcome)
        {
            if (outcome.Succeeded) return "FABRICADO · +1 POCIÓN DE SALUD · −2 HIERBAS · −1 RESINA";
            switch (outcome.Status)
            {
                case CraftingOperationStatus.RejectedInsufficientMaterials:
                    return "RECHAZADO · faltan materiales; bolsa sin cambios.";
                case CraftingOperationStatus.RejectedInsufficientGold:
                    return "RECHAZADO · falta oro; bolsa sin cambios.";
                case CraftingOperationStatus.RejectedBackpackFull:
                    return "RECHAZADO · la mochila está llena; bolsa sin cambios.";
                default:
                    return "RECHAZADO · " + outcome.Status + "; bolsa sin cambios.";
            }
        }
    }
}
