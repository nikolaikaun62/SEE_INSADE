using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace SEE_INSADE.Core.Localization
{
    public sealed class LocalizationManager
    {
        public static LocalizationManager Instance { get; } = new();

        private readonly Dictionary<string, Dictionary<string, string>> _languages = new();

        public ObservableCollection<LanguageOption> AvailableLanguages { get; } = new();
        public string CurrentLanguage { get; private set; } = "en";

        public void LoadLanguages()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "Languages");
            Directory.CreateDirectory(directory);

            _languages.Clear();
            AvailableLanguages.Clear();

            foreach (string file in Directory.EnumerateFiles(directory, "*.json"))
            {
                TryLoadLanguage(file);
            }

            if (!_languages.ContainsKey("en") && _languages.Count > 0)
            {
                foreach (string code in _languages.Keys)
                {
                    CurrentLanguage = code;
                    break;
                }
            }
        }

        public bool SetLanguage(string code)
        {
            if (!_languages.ContainsKey(code))
                return false;

            CurrentLanguage = code;
            return true;
        }

        public string T(string key)
        {
            if (_languages.TryGetValue(CurrentLanguage, out Dictionary<string, string>? current) &&
                current.TryGetValue(key, out string? value))
                return value;

            if (_languages.TryGetValue("en", out Dictionary<string, string>? english) &&
                english.TryGetValue(key, out string? fallback))
                return fallback;

            return key;
        }

        public string TText(string englishText)
        {
            if (!_languages.TryGetValue("en", out Dictionary<string, string>? english))
                return englishText;

            foreach (var pair in english)
            {
                if (!pair.Value.Equals(englishText, StringComparison.Ordinal))
                    continue;

                return T(pair.Key);
            }

            return englishText;
        }

        private void TryLoadLanguage(string file)
        {
            try
            {
                string json = File.ReadAllText(file);
                var document = JsonSerializer.Deserialize<LanguageFile>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (document?.Code == null || document.Strings == null)
                    return;

                _languages[document.Code] = document.Strings;
                AvailableLanguages.Add(new LanguageOption(document.Code, document.Name ?? document.Code));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Language load failed for {file}: {ex.Message}");
            }
        }

        private sealed class LanguageFile
        {
            public string? Code { get; set; }
            public string? Name { get; set; }
            public Dictionary<string, string>? Strings { get; set; }
        }
    }
}
