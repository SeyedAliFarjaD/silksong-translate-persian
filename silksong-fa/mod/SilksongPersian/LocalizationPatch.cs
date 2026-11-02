using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using TeamCherry.Localization;
using UnityEngine;

namespace SilksongPersian
{
    /// <summary>
    /// Teaches the game a Persian language without touching its assets.
    ///
    /// The game builds its language list by walking the LanguageCode enum and keeping every code
    /// that has a sheet file; FA (Faroese) ships no sheet, so we claim it. Discovery can run
    /// before this plugin is patched, so the code is asserted from three places: the list builder
    /// itself, the language loader that runs right after it, and a short watchdog in the plugin.
    ///
    /// These are plain static methods; Patcher attaches them explicitly.
    /// </summary>
    internal static class LocalizationPatch
    {
        internal const string PersianCode = "FA";

        private static bool _reported;

        private static readonly Dictionary<string, Dictionary<string, string>> EnglishCache =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        private static readonly Regex EntryRegex = new Regex(
            "<entry\\s+name=\"([^\"]*)\"\\s*>(.*?)</entry>",
            RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// Sheet titles the game may request whose English asset is registered under another name.
        /// </summary>
        private static readonly Dictionary<string, string> SheetAliases =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Hornet Old", "UI" },
            };

        private static bool _missingLogged;

        /// <summary>
        /// Safety net: the game renders a missing entry as "#!#KEY#!#". Whenever that happens the
        /// English wording is substituted, so an untranslated or unavailable sheet degrades to
        /// English instead of exposing raw keys.
        /// </summary>
        internal static void GetPostfix(string key, string sheetTitle, ref string __result)
        {
            if (string.IsNullOrEmpty(__result)) return;
            if (!__result.StartsWith("#!#", StringComparison.Ordinal)) return;

            string english = EnglishLookup(sheetTitle, key);
            if (english != null)
            {
                __result = english;
                if (!_missingLogged)
                {
                    _missingLogged = true;
                    Plugin.Log.LogInfo($"Missing '{key}' in sheet '{sheetTitle}'; using English.");
                }
                return;
            }

            if (!_missingLogged)
            {
                _missingLogged = true;
                Plugin.Log.LogWarning($"Key '{key}' is missing from sheet '{sheetTitle}' in both languages.");
            }
            __result = key;
        }

        private static string EnglishLookup(string sheetTitle, string key)
        {
            if (!EnglishCache.TryGetValue(sheetTitle, out var entries))
            {
                entries = LoadEnglishEntries(sheetTitle);
                EnglishCache[sheetTitle] = entries;
            }

            return entries.TryGetValue(key, out string value) ? value : null;
        }

        private static Dictionary<string, string> LoadEnglishEntries(string sheetTitle)
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);

            string xml = EnglishSheet(sheetTitle);
            if (xml == null && SheetAliases.TryGetValue(sheetTitle, out string alias))
            {
                xml = EnglishSheet(alias);
            }
            if (xml == null) return entries;

            foreach (Match match in EntryRegex.Matches(xml))
            {
                entries[match.Groups[1].Value] = UnescapeXml(match.Groups[2].Value);
            }
            return entries;
        }

        private static string UnescapeXml(string value) =>
            value.Replace("&apos;", "'").Replace("&quot;", "\"")
                 .Replace("&gt;", ">").Replace("&lt;", "<").Replace("&amp;", "&");

        internal static bool HasLanguageFilePrefix(string lang, ref bool __result)
        {
            if (lang == PersianCode)
            {
                __result = true;
                return false;
            }
            return true;
        }

        internal static void LoadAvailableLanguagesPostfix()
        {
            EnsurePersianListed("LoadAvailableLanguages");
            ReportList();
        }

        internal static void LoadLanguagePrefix()
        {
            EnsurePersianListed("LoadLanguage");
        }

        /// <summary>
        /// Optionally force Persian on startup so the translation is visible immediately, without
        /// hunting through the options menu. Disable it to keep the game's own saved language.
        /// </summary>
        internal static void RestoreLanguageSelectionPostfix(ref string __result)
        {
            if (Plugin.ForcePersian != null && Plugin.ForcePersian.Value)
            {
                __result = PersianCode;
            }
        }

        private static bool _switchAttempted;

        /// <summary>
        /// The game finishes its language setup during plugin load, before these patches exist,
        /// so the switch is performed once from the watchdog instead of relying on a hook.
        /// </summary>
        internal static void EnsurePersianActive()
        {
            if (_switchAttempted) return;
            if (Plugin.ForcePersian == null || !Plugin.ForcePersian.Value) return;

            if (Language.CurrentLanguage().ToString() == PersianCode)
            {
                _switchAttempted = true;
                return;
            }

            System.Reflection.MethodInfo switchMethod;
            try
            {
                switchMethod = AccessTools.Method(typeof(Language), "SwitchLanguage", new[] { typeof(string) });
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not resolve Language.SwitchLanguage: {e.Message}");
                _switchAttempted = true;
                return;
            }

            if (switchMethod == null)
            {
                Plugin.Log.LogError("Language.SwitchLanguage(string) was not found; cannot activate Persian.");
                _switchAttempted = true;
                return;
            }

            try
            {
                switchMethod.Invoke(null, new object[] { PersianCode });
                _switchAttempted = true;
                Plugin.Log.LogInfo($"Switched the active language to {PersianCode}.");
            }
            catch (Exception e)
            {
                _switchAttempted = true;
                Plugin.Log.LogError($"Could not switch to Persian: {e}");
            }
        }

        /// <summary>Add FA to the discovered-language list if it is missing.</summary>
        internal static void EnsurePersianListed(string source)
        {
            try
            {
                var field = AccessTools.Field(typeof(Language), "_availableLanguages");
                if (field == null)
                {
                    if (!_reported) Plugin.Log.LogError("Could not find Language._availableLanguages.");
                    return;
                }

                if (!(field.GetValue(null) is List<string> languages))
                {
                    // The list does not exist yet; a later hook will catch it.
                    return;
                }

                if (languages.Contains(PersianCode)) return;

                languages.Add(PersianCode);
                Plugin.Log.LogInfo($"Persian (FA) added to the language list via {source}.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not extend the language list: {e}");
            }
        }

        private static void ReportList()
        {
            if (_reported) return;
            var field = AccessTools.Field(typeof(Language), "_availableLanguages");
            if (!(field?.GetValue(null) is List<string> languages)) return;
            _reported = true;
            Plugin.Log.LogInfo("Available languages: " + string.Join(", ", languages.ToArray()));
        }

        internal static bool GetLanguageFileContentsPrefix(string sheetTitle, ref string __result)
        {
            if (Language.CurrentLanguage().ToString() != PersianCode) return true;

            string english = EnglishSheet(sheetTitle);
            string merged = TranslationStore.BuildMergedSheetXml(sheetTitle, english, out int persianKeys);

            if (merged == null)
            {
                Plugin.Log.LogWarning($"Sheet '{sheetTitle}': no English or Persian content available.");
            }
            else if (persianKeys > 0)
            {
                Plugin.Log.LogInfo($"Sheet '{sheetTitle}': {persianKeys} Persian key(s) over English.");
            }

            __result = merged ?? english ?? string.Empty;
            return false;
        }

        private static string EnglishSheet(string sheetTitle)
        {
            var asset = Resources.Load<TextAsset>("Languages/EN_" + sheetTitle);
            if (asset == null) return null;
            try
            {
                return TeamCherry.SharedUtils.Encryption.Decrypt(asset.text);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not decrypt the English sheet '{sheetTitle}': {e.Message}");
                return null;
            }
        }
    }
}
