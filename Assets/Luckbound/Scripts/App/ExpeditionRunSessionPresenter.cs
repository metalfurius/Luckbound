using System;
using System.Linq;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Inventory;
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
            InventoryItemStack quickConsumable = run.Inventory.QuickConsumable;
            _summaryText.text = "RAZA · HUMANO\nHABILIDADES · " + string.Join("  ·  ", run.SkillIds.Select(SkillLabel)) +
                "\nARMA · " + (run.Inventory.Weapon?.ItemId ?? "sin equipar") +
                "\nARMADURA · " + (run.Inventory.Armor?.ItemId ?? "sin equipar") +
                "\nPOCIONES · " + (quickConsumable == null ? 0 : quickConsumable.Quantity) +
                "\nSALUD / ENERGÍA · " + run.StartingHealth + " / " + run.StartingEnergy +
                "\nFORTUNA · " + run.StartingFortunaCharges +
                "\nRUTA · " + run.Route.Nodes.Count + " nodos · semilla " + run.Seed.ToString("X8") +
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
