using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace SilksongPersian
{
    /// <summary>
    /// Attaches every patch explicitly and logs the outcome of each one.
    ///
    /// Attribute-based PatchAll silently attached nothing in this environment (all targets
    /// resolved, yet Harmony reported zero attached patches), so patching is done by hand where
    /// each result is observable.
    /// </summary>
    internal static class Patcher
    {
        internal static void Apply(Harmony harmony)
        {
            // Modern TextMeshPro (UI, menus, tooltips, cards, shop)
            Bind(harmony, typeof(TMP_Text), "SetText",
                prefix: AccessTools.Method(typeof(TmpPatch), nameof(TmpPatch.SetTextPrefix)),
                argumentTypes: new[] { typeof(string) });

            Bind(harmony, typeof(TMP_Text), "SetText",
                prefix: AccessTools.Method(typeof(TmpPatch), nameof(TmpPatch.SetTextBoolPrefix)),
                argumentTypes: new[] { typeof(string), typeof(bool) });

            Bind(harmony, typeof(TextMeshProUGUI), "set_text",
                prefix: AccessTools.Method(typeof(TmpPatch), nameof(TmpPatch.UguiSetTextPrefix)));

            Bind(harmony, typeof(TextMeshPro), "set_text",
                prefix: AccessTools.Method(typeof(TmpPatch), nameof(TmpPatch.TmpSetTextPrefix)));

            Bind(harmony, typeof(TMP_Text), "set_isRightToLeftText",
                prefix: AccessTools.Method(typeof(TmpPatch), nameof(TmpPatch.SetRightToLeftPrefix)));

            Bind(harmony, typeof(TMP_Text), "set_alignment",
                prefix: AccessTools.Method(typeof(TmpPatch), nameof(TmpPatch.SetAlignmentPrefix)),
                argumentTypes: new[] { typeof(TextAlignmentOptions) });

            // TMProOld (embedded legacy TextMeshPro in Assembly-CSharp, used in DialogueBox)
            Bind(harmony, typeof(TMProOld.TMP_Text), "set_text",
                prefix: AccessTools.Method(typeof(RtlPatch), nameof(RtlPatch.TmProOldSetTextPrefix)));

            Bind(harmony, typeof(TMProOld.TMP_Text), "SetText",
                prefix: AccessTools.Method(typeof(RtlPatch), nameof(RtlPatch.TmProOldSetTextStringPrefix)),
                argumentTypes: new[] { typeof(string) });

            Bind(harmony, typeof(TMProOld.TMP_Text), "set_alignment",
                prefix: AccessTools.Method(typeof(RtlPatch), nameof(RtlPatch.TmProOldSetAlignmentPrefix)),
                argumentTypes: new[] { typeof(TMProOld.TextAlignmentOptions) });

            // DialogueBox (NPC & player dialogues)
            Bind(harmony, typeof(DialogueBox), "ParseTextForDialogueLines",
                prefix: AccessTools.Method(typeof(RtlPatch), nameof(RtlPatch.DialogueBoxParseTextPrefix)),
                argumentTypes: new[] { typeof(string) });

            Bind(harmony, typeof(DialogueBox), "ShowShared",
                postfix: AccessTools.Method(typeof(RtlPatch), nameof(RtlPatch.DialogueBoxShowSharedPostfix)));

            Bind(harmony, typeof(DialogueBox), "PrintText",
                prefix: AccessTools.Method(typeof(RtlPatch), nameof(RtlPatch.DialogueBoxPrintTextPrefix)),
                argumentTypes: new[] { typeof(TMProOld.TMP_TextInfo), typeof(int) });
        }

        private static void Bind(Harmony harmony, Type type, string name,
            System.Reflection.MethodInfo prefix = null,
            System.Reflection.MethodInfo postfix = null,
            Type[] argumentTypes = null)
        {
            var target = argumentTypes == null
                ? AccessTools.Method(type, name)
                : AccessTools.Method(type, name, argumentTypes);

            string label = $"{type.Name}.{name}";
            if (target == null)
            {
                Plugin.Log.LogError($"PATCH FAILED: {label} (target method not found)");
                return;
            }

            try
            {
                harmony.Patch(
                    target,
                    prefix: prefix == null ? null : new HarmonyMethod(prefix),
                    postfix: postfix == null ? null : new HarmonyMethod(postfix));

                var info = Harmony.GetPatchInfo(target);
                int attached = (info?.Prefixes.Count ?? 0) + (info?.Postfixes.Count ?? 0);
                Plugin.Log.LogInfo($"PATCH OK: {label} (attached {attached})");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"PATCH FAILED: {label}: {e.Message}");
            }
        }
    }
}
