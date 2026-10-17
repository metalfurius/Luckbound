using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Luckbound.App
{
    /// <summary>
    /// Presents an already-persisted Fortune offer. The encounter owner supplies its offer ID;
    /// this component never creates offers or assigns reward content.
    /// </summary>
    public sealed class FortuneOfferPanelController : MonoBehaviour
    {
        [SerializeField] private Button[] _optionButtons = new Button[3];
        [SerializeField] private Text[] _optionLabels = new Text[3];
        [SerializeField] private Button _repeatButton;
        [SerializeField] private Button _claimButton;
        [SerializeField] private Text _repeatAvailabilityText;
        [SerializeField] private Text _statusText;

        private ApplicationBootstrapper _application;
        private string _offerId;
        private UnityAction[] _optionHandlers;
        private UnityAction _repeatHandler;
        private UnityAction _claimHandler;
        private string _pendingChoiceOptionId = string.Empty;
        private bool _pendingClaimRetry;
        private bool _pendingRerollRetry;
        private TransactionId _pendingRerollTransactionId;
        private bool _busy;
        private bool _initialized;
        private bool _handlersBound;

        public FortuneOffer CurrentOffer { get; private set; }
        public FortuneRerollAvailability RerollAvailability { get; private set; } =
            FortuneRerollAvailability.UnknownOffer;
        public TransactionId LastRerollTransactionId { get; private set; }
        public bool HasPendingRerollRetry => _pendingRerollRetry;
        public bool IsBusy => _busy;
        public string RepeatAvailabilityText => _repeatAvailabilityText == null
            ? string.Empty
            : _repeatAvailabilityText.text;
        public string StatusText => _statusText == null ? string.Empty : _statusText.text;

        private void OnEnable()
        {
            BindButtonHandlers();
        }

        private void OnDisable()
        {
            UnbindButtonHandlers();
        }

        /// <summary>Uses references assigned through the Inspector.</summary>
        public void Initialize(ApplicationBootstrapper application, string offerId)
        {
            Initialize(application, offerId, _optionButtons, _optionLabels, _repeatButton, _claimButton,
                _repeatAvailabilityText, _statusText);
        }

        /// <summary>
        /// Binds an existing offer and its three option controls. This overload also supports
        /// runtime-created panels and tests without requiring a scene or prefab edit.
        /// </summary>
        public void Initialize(ApplicationBootstrapper application, string offerId,
            Button[] optionButtons, Text[] optionLabels, Button repeatButton, Button claimButton,
            Text repeatAvailabilityText, Text statusText)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));
            if (string.IsNullOrWhiteSpace(offerId)) throw new ArgumentException("A committed offer ID is required.", nameof(offerId));
            if (optionButtons == null || optionButtons.Length != 3)
                throw new ArgumentException("Exactly three Fortune option buttons are required.", nameof(optionButtons));
            if (optionLabels == null || optionLabels.Length != 3)
                throw new ArgumentException("Exactly three Fortune option labels are required.", nameof(optionLabels));
            for (int index = 0; index < 3; index++)
            {
                if (optionButtons[index] == null) throw new ArgumentException("A Fortune option button is missing.", nameof(optionButtons));
                if (optionLabels[index] == null) throw new ArgumentException("A Fortune option label is missing.", nameof(optionLabels));
            }
            if (repeatButton == null) throw new ArgumentNullException(nameof(repeatButton));
            if (claimButton == null) throw new ArgumentNullException(nameof(claimButton));
            if (repeatAvailabilityText == null) throw new ArgumentNullException(nameof(repeatAvailabilityText));
            if (statusText == null) throw new ArgumentNullException(nameof(statusText));

            UnbindButtonHandlers();
            _application = application;
            _offerId = offerId;
            _optionButtons = (Button[])optionButtons.Clone();
            _optionLabels = (Text[])optionLabels.Clone();
            _repeatButton = repeatButton;
            _claimButton = claimButton;
            _repeatAvailabilityText = repeatAvailabilityText;
            _statusText = statusText;
            _pendingChoiceOptionId = string.Empty;
            _pendingClaimRetry = false;
            _pendingRerollRetry = false;
            _pendingRerollTransactionId = default;
            LastRerollTransactionId = default;
            _busy = false;
            _initialized = true;
            _statusText.text = string.Empty;
            BindButtonHandlers();
            Refresh();
            RestorePendingChoiceRetry();
        }

        /// <summary>Reloads the current committed offer and its Core-provided reroll state.</summary>
        public void Refresh()
        {
            if (!_initialized) return;

            CurrentOffer = _application.GetFortuneOffer(_offerId);
            RerollAvailability = _application.GetFortuneRerollAvailability(_offerId);
            _repeatAvailabilityText.text = RerollAvailability.ToString();

            bool hasThreeOptions = CurrentOffer != null && CurrentOffer.OptionIds.Count == 3;
            bool claimed = CurrentOffer != null && CurrentOffer.IsClaimed;
            string selectedOptionId = CurrentOffer == null ? string.Empty : CurrentOffer.SelectedOptionId;
            for (int index = 0; index < 3; index++)
            {
                string optionId = hasThreeOptions ? CurrentOffer.OptionIds[index] : string.Empty;
                _optionLabels[index].text = optionId;
                bool retryingThisChoice = !string.IsNullOrEmpty(_pendingChoiceOptionId) &&
                    string.Equals(_pendingChoiceOptionId, optionId, StringComparison.Ordinal);
                bool selectionStillOpen = string.IsNullOrEmpty(selectedOptionId) || retryingThisChoice;
                _optionButtons[index].interactable = !_busy && hasThreeOptions && !claimed &&
                    !_pendingClaimRetry && !_pendingRerollRetry && selectionStillOpen &&
                    (string.IsNullOrEmpty(_pendingChoiceOptionId) || retryingThisChoice);
            }

            _repeatButton.interactable = !_busy && !_pendingClaimRetry &&
                string.IsNullOrEmpty(_pendingChoiceOptionId) && !claimed &&
                (_pendingRerollRetry || RerollAvailability == FortuneRerollAvailability.Available);
            bool hasCommittedChoice = CurrentOffer != null && !string.IsNullOrEmpty(selectedOptionId);
            _claimButton.interactable = !_busy && !_pendingRerollRetry &&
                string.IsNullOrEmpty(_pendingChoiceOptionId) &&
                (_pendingClaimRetry || (hasCommittedChoice && !claimed));
        }

        /// <summary>Repeats the supplied offer, retaining the transaction ID until its save succeeds.</summary>
        public async Task<ExpeditionMutationCommitResult<FortuneRerollResult>> RepeatOfferAsync()
        {
            if (!_initialized || _busy || _application == null) return null;
            if (!_pendingRerollRetry && TryRestorePendingRerollTransaction(out TransactionId pendingTransactionId))
            {
                _pendingRerollTransactionId = pendingTransactionId;
                LastRerollTransactionId = pendingTransactionId;
                _pendingRerollRetry = true;
            }
            if (_application.HasPendingFortuneMutation &&
                !PendingOperationMatches("offer.repeat", _offerId, _pendingRerollTransactionId.ToString()))
                return null;
            Refresh();
            if (!_pendingRerollRetry && RerollAvailability != FortuneRerollAvailability.Available) return null;
            if (_pendingClaimRetry || !string.IsNullOrEmpty(_pendingChoiceOptionId) ||
                CurrentOffer == null || CurrentOffer.IsClaimed || !string.IsNullOrEmpty(CurrentOffer.SelectedOptionId))
                return null;

            if (!_pendingRerollRetry)
                _pendingRerollTransactionId = TransactionId.New();
            _pendingRerollRetry = true;
            LastRerollTransactionId = _pendingRerollTransactionId;
            _busy = true;
            Refresh();
            try
            {
                ExpeditionMutationCommitResult<FortuneRerollResult> result =
                    await _application.RepeatFortuneOfferAsync(_offerId, _pendingRerollTransactionId);
                if (result != null)
                {
                    _pendingRerollRetry = !result.Persisted;
                    if (result.Persisted) _pendingRerollTransactionId = default;
                    _statusText.text = result.Message;
                }
                return result;
            }
            catch (Exception exception)
            {
                _statusText.text = exception.Message;
                return null;
            }
            finally
            {
                _busy = false;
                Refresh();
            }
        }

        /// <summary>Persists one of the three displayed choices before enabling its claim.</summary>
        public async Task<ExpeditionMutationCommitResult<FortuneChoiceResult>> ChooseOptionAsync(int optionIndex)
        {
            if (!_initialized || _busy || _application == null || CurrentOffer == null ||
                CurrentOffer.OptionIds.Count != 3 || optionIndex < 0 || optionIndex >= 3 ||
                _pendingRerollRetry || _pendingClaimRetry || CurrentOffer.IsClaimed)
                return null;

            string optionId = CurrentOffer.OptionIds[optionIndex];
            if (!string.IsNullOrEmpty(_pendingChoiceOptionId) &&
                !string.Equals(_pendingChoiceOptionId, optionId, StringComparison.Ordinal)) return null;
            if (string.IsNullOrEmpty(_pendingChoiceOptionId) && !string.IsNullOrEmpty(CurrentOffer.SelectedOptionId))
                return null;
            if (_application.HasPendingFortuneMutation &&
                !PendingOperationMatches("offer.choose", _offerId, optionId))
                return null;

            _pendingChoiceOptionId = optionId;
            _busy = true;
            Refresh();
            try
            {
                ExpeditionMutationCommitResult<FortuneChoiceResult> result =
                    await _application.ChooseFortuneOptionAsync(_offerId, optionId);
                if (result != null)
                {
                    if (result.Persisted) _pendingChoiceOptionId = string.Empty;
                    _statusText.text = result.Message;
                }
                return result;
            }
            catch (Exception exception)
            {
                _statusText.text = exception.Message;
                return null;
            }
            finally
            {
                _busy = false;
                Refresh();
            }
        }

        /// <summary>Claims the selected item through the bootstrapper's atomic, idempotent reward API.</summary>
        public async Task<ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome>> ClaimRewardAsync()
        {
            if (!_initialized || _busy || _application == null || _pendingRerollRetry ||
                !string.IsNullOrEmpty(_pendingChoiceOptionId))
                return null;
            Refresh();
            if (!_pendingClaimRetry && (CurrentOffer == null || CurrentOffer.IsClaimed ||
                string.IsNullOrEmpty(CurrentOffer.SelectedOptionId)))
                return null;
            if (_application.HasPendingFortuneMutation &&
                !PendingOperationMatches("offer.claim.reward", _offerId))
                return null;

            _busy = true;
            _pendingClaimRetry = true;
            Refresh();
            try
            {
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> result =
                    await _application.ClaimFortuneOfferRewardAsync(_offerId);
                if (result != null)
                {
                    _pendingClaimRetry = !result.Persisted;
                    _statusText.text = result.Message;
                }
                return result;
            }
            catch (Exception exception)
            {
                _statusText.text = exception.Message;
                return null;
            }
            finally
            {
                _busy = false;
                Refresh();
            }
        }

        private void BindButtonHandlers()
        {
            if (!_initialized || _handlersBound) return;
            _optionHandlers = new UnityAction[3];
            for (int index = 0; index < 3; index++)
            {
                int capturedIndex = index;
                _optionHandlers[index] = () => OnOptionButtonClicked(capturedIndex);
                _optionButtons[index].onClick.AddListener(_optionHandlers[index]);
            }
            _repeatHandler = OnRepeatButtonClicked;
            _claimHandler = OnClaimButtonClicked;
            _repeatButton.onClick.AddListener(_repeatHandler);
            _claimButton.onClick.AddListener(_claimHandler);
            _handlersBound = true;
        }

        private void UnbindButtonHandlers()
        {
            if (!_handlersBound) return;
            for (int index = 0; index < _optionHandlers.Length; index++)
                if (_optionButtons[index] != null && _optionHandlers[index] != null)
                    _optionButtons[index].onClick.RemoveListener(_optionHandlers[index]);
            if (_repeatButton != null && _repeatHandler != null) _repeatButton.onClick.RemoveListener(_repeatHandler);
            if (_claimButton != null && _claimHandler != null) _claimButton.onClick.RemoveListener(_claimHandler);
            _optionHandlers = null;
            _repeatHandler = null;
            _claimHandler = null;
            _handlersBound = false;
        }

        private void OnOptionButtonClicked(int index)
        {
            _ = ChooseOptionAsync(index);
        }

        private void OnRepeatButtonClicked()
        {
            _ = RepeatOfferAsync();
        }

        private void OnClaimButtonClicked()
        {
            _ = ClaimRewardAsync();
        }

        private bool TryRestorePendingRerollTransaction(out TransactionId transactionId)
        {
            transactionId = default;
            if (!_application.HasPendingFortuneMutation) return false;
            if (!TryParseOperationKey(_application.PendingFortuneOperationKey, out string[] fields) ||
                fields.Length != 3 || !string.Equals(fields[0], "offer.repeat", StringComparison.Ordinal) ||
                !string.Equals(fields[1], _offerId, StringComparison.Ordinal) ||
                !TransactionId.TryParse(fields[2], out transactionId))
                return false;

            return true;
        }

        private void RestorePendingChoiceRetry()
        {
            if (!_application.HasPendingFortuneMutation || CurrentOffer == null || CurrentOffer.IsClaimed ||
                !string.IsNullOrEmpty(CurrentOffer.SelectedOptionId) ||
                !TryParseOperationKey(_application.PendingFortuneOperationKey, out string[] fields) ||
                fields.Length != 3 || !string.Equals(fields[0], "offer.choose", StringComparison.Ordinal) ||
                !string.Equals(fields[1], _offerId, StringComparison.Ordinal))
            {
                return;
            }

            foreach (string optionId in CurrentOffer.OptionIds)
            {
                if (!string.Equals(optionId, fields[2], StringComparison.Ordinal)) continue;
                _pendingChoiceOptionId = optionId;
                _statusText.text = "GUARDADO PENDIENTE · vuelve a elegir la misma opción para reintentar.";
                Refresh();
                return;
            }
        }

        private bool PendingOperationMatches(string operation, params string[] arguments)
        {
            if (!_application.HasPendingFortuneMutation ||
                !TryParseOperationKey(_application.PendingFortuneOperationKey, out string[] fields) ||
                fields.Length != arguments.Length + 1 || !string.Equals(fields[0], operation, StringComparison.Ordinal))
                return false;
            for (int index = 0; index < arguments.Length; index++)
                if (!string.Equals(fields[index + 1], arguments[index], StringComparison.Ordinal)) return false;
            return true;
        }

        private static bool TryParseOperationKey(string operationKey, out string[] fields)
        {
            fields = Array.Empty<string>();
            if (string.IsNullOrEmpty(operationKey)) return false;
            var parsedFields = new List<string>();
            int offset = 0;
            while (offset < operationKey.Length)
            {
                if (!TryReadOperationKeyField(operationKey, ref offset, out string field)) return false;
                parsedFields.Add(field);
            }
            fields = parsedFields.ToArray();
            return fields.Length != 0;
        }

        private static bool TryReadOperationKeyField(string operationKey, ref int offset, out string field)
        {
            field = string.Empty;
            if (string.IsNullOrEmpty(operationKey) || offset < 0 || offset >= operationKey.Length) return false;
            int separator = operationKey.IndexOf(':', offset);
            if (separator < 0 || !int.TryParse(operationKey.Substring(offset, separator - offset),
                NumberStyles.None, CultureInfo.InvariantCulture, out int length) || length < 0)
                return false;

            int valueStart = separator + 1;
            if (length > operationKey.Length - valueStart) return false;
            field = operationKey.Substring(valueStart, length);
            offset = valueStart + length;
            return true;
        }
    }
}
