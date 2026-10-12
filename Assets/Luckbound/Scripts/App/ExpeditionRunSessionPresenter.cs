using System;
using System.Linq;
using Luckbound.Core.Expeditions;
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

        private void OnEnable()
        {
            if (_returnToTavernButton != null) _returnToTavernButton.onClick.AddListener(ReturnToTavern);
        }

        private void Start()
        {
            if (_overlayRoot == null || _titleText == null || _summaryText == null || _returnToTavernButton == null)
                throw new InvalidOperationException("ExpeditionRunSessionPresenter is missing a serialized UI reference.");

            ExpeditionRunConfiguration run = ApplicationBootstrapper.Instance == null
                ? null
                : ApplicationBootstrapper.Instance.ActiveRun;
            if (run == null)
            {
                _overlayRoot.SetActive(false);
                return;
            }

            _titleText.text = "EXPEDICIÓN PREPARADA · " + ClassLabel(run.ClassId);
            ExpeditionSaveData saved = ApplicationBootstrapper.Instance.ActiveExpedition;
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
                "\nRUTA · " + run.Route.Nodes.Count + " nodos · semilla " + run.Seed.ToString("X8") +
                (saved == null ? string.Empty : "\nSALA · " + saved.currentNodeId + " · " + saved.completedRoomIds.Length + " completadas" +
                    (saved.completedRoomIds.Contains(saved.currentNodeId) ? " · resultado guardado" : " · checkpoint de entrada guardado")) +
                "\nID · " + run.RunId;
            _overlayRoot.SetActive(true);
        }

        private void OnDisable()
        {
            if (_returnToTavernButton != null) _returnToTavernButton.onClick.RemoveListener(ReturnToTavern);
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
    }
}
