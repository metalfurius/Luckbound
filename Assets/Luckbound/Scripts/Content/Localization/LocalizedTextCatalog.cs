using System;
using System.Collections.Generic;

namespace Luckbound.Content
{
    /// <summary>
    /// An immutable collection of the authored Spanish and English text entries.
    /// A missing key resolves to the key itself so content errors remain stable and visible.
    /// </summary>
    public sealed class LocalizedTextCatalog
    {
        private readonly Dictionary<string, LocalizedTextEntry> _entries;

        internal LocalizedTextCatalog(string defaultLanguageId, IDictionary<string, LocalizedTextEntry> entries)
        {
            LocalizationLanguage.RequireSupported(defaultLanguageId, nameof(defaultLanguageId));
            if (entries == null) throw new ArgumentNullException(nameof(entries));

            DefaultLanguageId = defaultLanguageId;
            _entries = new Dictionary<string, LocalizedTextEntry>(entries, StringComparer.Ordinal);
        }

        public string DefaultLanguageId { get; }

        public int Count => _entries.Count;

        /// <summary>
        /// Returns text for the requested language. If the key is not authored, returns the key unchanged.
        /// </summary>
        public string GetText(string key, string languageId)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Localization key cannot be empty.", nameof(key));
            LocalizationLanguage.RequireSupported(languageId, nameof(languageId));

            if (!_entries.TryGetValue(key, out LocalizedTextEntry entry)) return key;
            return entry.Get(languageId);
        }
    }

    internal sealed class LocalizedTextEntry
    {
        private readonly string _spanish;
        private readonly string _english;

        internal LocalizedTextEntry(string spanish, string english)
        {
            _spanish = spanish;
            _english = english;
        }

        internal string Get(string languageId)
        {
            return languageId == LocalizationLanguage.Spanish ? _spanish : _english;
        }
    }

    internal static class LocalizationLanguage
    {
        internal const string Spanish = "es";
        internal const string English = "en";

        internal static void RequireSupported(string languageId, string parameterName)
        {
            if (languageId != Spanish && languageId != English)
            {
                throw new ArgumentException("Language must be exactly 'es' or 'en'.", parameterName);
            }
        }

        internal static string ResolveInitial(string languageId, string defaultLanguageId)
        {
            if (string.IsNullOrEmpty(languageId)) return defaultLanguageId;
            RequireSupported(languageId, nameof(languageId));
            return languageId;
        }
    }
}