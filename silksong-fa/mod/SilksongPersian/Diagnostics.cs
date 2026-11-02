using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace SilksongPersian
{
    /// <summary>
    /// Reports, at load time, which of the intended Harmony targets resolved and whether our
    /// patch is actually attached to them. A silent patch that binds to nothing looks identical
    /// to a working one from the outside, so this is logged every run.
    /// </summary>
    internal static class Diagnostics
    {
        internal static void Report()
        {
            try
            {
                var languageAsm = typeof(TeamCherry.Localization.Language).Assembly;
                Plugin.Log.LogInfo($"Language type from: {languageAsm.GetName().Name} @ {languageAsm.Location}");

                Check(typeof(TeamCherry.Localization.Language), "HasLanguageFile");
                Check(typeof(TeamCherry.Localization.Language), "LoadAvailableLanguages");
                Check(typeof(TeamCherry.Localization.Language), "GetLanguageFileContents");

                Check(typeof(TMP_Text), "SetText", new[] { typeof(string) });
                Check(typeof(TMP_Text), "SetText", new[] { typeof(string), typeof(bool) });
                Check(typeof(TMP_Text), "set_isRightToLeftText");
                Check(typeof(TextMeshProUGUI), "set_text");
                Check(typeof(TextMeshPro), "set_text");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Diagnostics failed: {e}");
            }
        }

        private static void Check(Type type, string name, Type[] parameters = null)
        {
            var method = parameters == null
                ? AccessTools.Method(type, name)
                : AccessTools.Method(type, name, parameters);

            if (method == null)
            {
                Plugin.Log.LogError($"TARGET MISSING: {type.FullName}.{name}");
                return;
            }

            var info = Harmony.GetPatchInfo(method);
            int prefixes = info?.Prefixes.Count ?? 0;
            int postfixes = info?.Postfixes.Count ?? 0;
            string owner = method.DeclaringType?.Assembly.GetName().Name;
            Plugin.Log.LogInfo(
                $"TARGET {type.Name}.{name}: prefixes={prefixes} postfixes={postfixes} (declared in {owner})");
        }
    }
}
