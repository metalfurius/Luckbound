using System;
using System.Collections.Generic;
using Luckbound.Content;
using Luckbound.Core.Saves;
using NUnit.Framework;

namespace Luckbound.Tests.Localization
{
    public sealed class LocalizationJsonLoaderTests
    {
        private const string BasicJson =
            "{\"schemaVersion\":1,\"defaultLanguageId\":\"es\",\"entries\":[{\"key\":\"menu.play\",\"es\":\"Jugar\",\"en\":\"Play\"}]}";

        [Test]
        public void LoadsCatalogAndChangesLanguageImmediatelyInMemory()
        {
            LocalizedTextCatalog catalog = LocalizationJsonLoader.Load(BasicJson);
            var settings = new GameSettingsSaveData { languageId = "es" };
            var localization = new LocalizationService(catalog, settings.languageId);
            var observedLanguages = new List<string>();
            localization.LanguageChanged += observedLanguages.Add;

            Assert.That(localization.LanguageId, Is.EqualTo("es"));
            Assert.That(localization.GetText("menu.play"), Is.EqualTo("Jugar"));

            localization.SetLanguage("en");

            Assert.That(localization.LanguageId, Is.EqualTo("en"));
            Assert.That(localization.GetText("menu.play"), Is.EqualTo("Play"));
            Assert.That(observedLanguages, Is.EqualTo(new[] { "en" }));

            localization.SetLanguage("en");
            Assert.That(observedLanguages, Is.EqualTo(new[] { "en" }));

            localization.SetLanguage("es");

            Assert.That(localization.GetText("menu.play"), Is.EqualTo("Jugar"));
            Assert.That(observedLanguages, Is.EqualTo(new[] { "en", "es" }));
        }

        [Test]
        public void UsesDeclaredDefaultWhenInitialLanguageIsOmitted()
        {
            string englishDefault = ReplaceOnce(BasicJson, "\"defaultLanguageId\":\"es\"", "\"defaultLanguageId\":\"en\"");
            LocalizedTextCatalog catalog = LocalizationJsonLoader.Load(englishDefault);

            Assert.That(new LocalizationService(catalog).LanguageId, Is.EqualTo("en"));
            Assert.That(new LocalizationService(catalog, string.Empty).GetText("menu.play"), Is.EqualTo("Play"));
        }

        [Test]
        public void ReturnsTheStableKeyWhenAnEntryIsMissing()
        {
            var localization = new LocalizationService(LocalizationJsonLoader.Load(BasicJson), "es");

            Assert.That(localization.GetText("story.not_loaded"), Is.EqualTo("story.not_loaded"));
        }

        [Test]
        public void RejectsFutureSchemaAndUnsupportedLanguages()
        {
            string futureSchema = ReplaceOnce(BasicJson, "\"schemaVersion\":1", "\"schemaVersion\":2");
            string unsupportedDefault = ReplaceOnce(BasicJson, "\"defaultLanguageId\":\"es\"", "\"defaultLanguageId\":\"fr\"");

            Assert.Throws<FormatException>(() => LocalizationJsonLoader.Load(futureSchema));
            Assert.Throws<FormatException>(() => LocalizationJsonLoader.Load(unsupportedDefault));
            Assert.Throws<ArgumentException>(() => new LocalizationService(LocalizationJsonLoader.Load(BasicJson), "fr"));

            var localization = new LocalizationService(LocalizationJsonLoader.Load(BasicJson), "es");
            Assert.Throws<ArgumentException>(() => localization.SetLanguage("fr"));
            Assert.That(localization.LanguageId, Is.EqualTo("es"));
        }

        [Test]
        public void RejectsDuplicateEmptyAndWhitespaceOnlyKeysOrTranslations()
        {
            string duplicateKey = ReplaceOnce(BasicJson,
                "\"entries\":[{\"key\":\"menu.play\"",
                "\"entries\":[{\"key\":\"menu.play\",\"es\":\"Jugar\",\"en\":\"Play\"},{\"key\":\"menu.play\"");
            string emptyKey = ReplaceOnce(BasicJson, "\"key\":\"menu.play\"", "\"key\":\"\"");
            string emptySpanish = ReplaceOnce(BasicJson, "\"es\":\"Jugar\"", "\"es\":\"\"");
            string whitespaceEnglish = ReplaceOnce(BasicJson, "\"en\":\"Play\"", "\"en\":\"   \"");

            Assert.Throws<FormatException>(() => LocalizationJsonLoader.Load(duplicateKey));
            Assert.Throws<FormatException>(() => LocalizationJsonLoader.Load(emptyKey));
            Assert.Throws<FormatException>(() => LocalizationJsonLoader.Load(emptySpanish));
            Assert.Throws<FormatException>(() => LocalizationJsonLoader.Load(whitespaceEnglish));
        }

        [Test]
        public void RejectsMissingUnknownAndDuplicateJsonFields()
        {
            string missingTranslation = ReplaceOnce(BasicJson, ",\"en\":\"Play\"", string.Empty);
            string unknownTopLevelField = ReplaceOnce(BasicJson, "\"entries\":[", "\"extra\":true,\"entries\":[");
            string duplicateTopLevelField = ReplaceOnce(BasicJson,
                "\"schemaVersion\":1",
                "\"schemaVersion\":1,\"schemaVersion\":1");
            string duplicateEntryField = ReplaceOnce(BasicJson,
                "\"key\":\"menu.play\"",
                "\"key\":\"menu.play\",\"key\":\"menu.play\"");

            Assert.Throws<FormatException>(() => LocalizationJsonLoader.Load(missingTranslation));
            Assert.Throws<FormatException>(() => LocalizationJsonLoader.Load(unknownTopLevelField));
            Assert.Throws<FormatException>(() => LocalizationJsonLoader.Load(duplicateTopLevelField));
            Assert.Throws<FormatException>(() => LocalizationJsonLoader.Load(duplicateEntryField));
            Assert.Throws<FormatException>(() => LocalizationJsonLoader.Load("{\"schemaVersion\":\"1\",\"defaultLanguageId\":\"es\",\"entries\":[]}"));
        }

        [Test]
        public void RejectsEmptyInputAndInvalidJson()
        {
            Assert.Throws<ArgumentException>(() => LocalizationJsonLoader.Load(string.Empty));
            Assert.Throws<FormatException>(() => LocalizationJsonLoader.Load("{not-json}"));
        }

        private static string ReplaceOnce(string input, string search, string replacement)
        {
            int index = input.IndexOf(search, StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Test fixture did not contain: " + search);
            return input.Substring(0, index) + replacement + input.Substring(index + search.Length);
        }
    }
}