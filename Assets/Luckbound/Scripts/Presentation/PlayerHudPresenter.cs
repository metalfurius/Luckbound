using System;
using System.Globalization;
using Luckbound.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Luckbound.Presentation
{
    /// <summary>Displays the resource snapshot without owning or mutating gameplay state.</summary>
    public sealed class PlayerHudPresenter : MonoBehaviour
    {
        private IPlayerHudStateSource _source;
        private Text _statusText;

        public string CurrentText => _statusText != null ? _statusText.text : string.Empty;

        public void Initialize(IPlayerHudStateSource source, Text statusText)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (statusText == null)
            {
                throw new ArgumentNullException(nameof(statusText));
            }

            _source = source;
            _statusText = statusText;
            Refresh();
        }

        public void Refresh()
        {
            if (_source == null || _statusText == null)
            {
                return;
            }

            PlayerHudSnapshot state = _source.CaptureHudState();
            string potionState = state.PotionPreparationTicksRemaining > 0
                ? " · PREPARANDO " + state.PotionPreparationTicksRemaining.ToString(CultureInfo.InvariantCulture) + " TICKS"
                : string.Empty;
            _statusText.text =
                "VIDA " + state.CurrentHealth.ToString(CultureInfo.InvariantCulture) + "/" +
                state.MaximumHealth.ToString(CultureInfo.InvariantCulture) +
                "   ENERGÍA " + state.CurrentEnergy.ToString(CultureInfo.InvariantCulture) + "/" +
                state.MaximumEnergy.ToString(CultureInfo.InvariantCulture) +
                "   POCIONES " + state.HealthPotionCharges.ToString(CultureInfo.InvariantCulture) + "/" +
                state.MaximumHealthPotionCharges.ToString(CultureInfo.InvariantCulture) + potionState +
                "\nF / LB · USAR POCIÓN     ESC / MENU · PAUSA";
        }

        private void LateUpdate()
        {
            Refresh();
        }
    }
}
