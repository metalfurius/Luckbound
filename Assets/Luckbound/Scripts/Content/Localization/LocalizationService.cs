using System;

namespace Luckbound.Content
{
    /// <summary>
    /// Resolves localized text for the current in-memory language. Application may initialize it from
    /// GameSettingsSaveData.languageId; the service does not persist a separate language preference.
    /// </summary>
    public sealed class LocalizationService
    {
        private readonly LocalizedTextCatalog _catalog;
        private string _languageId;

        public LocalizationService(LocalizedTextCatalog catalog, string initialLanguageId = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _languageId = LocalizationLanguage.ResolveInitial(initialLanguageId, catalog.DefaultLanguageId);
        }

        /// <summary>Raised after the active language changes, with the new language identifier.</summary>
        public event Action<string> LanguageChanged;

        public string LanguageId => _languageId;

        public string GetText(string key)
        {
            return _catalog.GetText(key, _languageId);
        }

        /// <summary>Changes language in memory and immediately affects subsequent GetText calls.</summary>
        public void SetLanguage(string languageId)
        {
            LocalizationLanguage.RequireSupported(languageId, nameof(languageId));
            if (string.Equals(_languageId, languageId, StringComparison.Ordinal)) return;

            _languageId = languageId;
            Action<string> handler = LanguageChanged;
            if (handler != null) handler(_languageId);
        }
    }
}