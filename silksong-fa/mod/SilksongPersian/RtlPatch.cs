using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMProOld;
using UnityEngine;

namespace SilksongPersian
{
    /// <summary>
    /// Enforces Right-to-Left (Right / TopRight) alignment and natural RTL typewriter reveal
    /// animation for Persian text across both TextMeshPro engines in Hollow Knight Silksong:
    ///   1. TMProOld (embedded legacy TMP used by DialogueBox and in-world text).
    ///   2. TMPro (modern TMP used by UI menus, inventory cards, tooltips, shops).
    ///
    /// Preserves Center alignment for buttons, titles, and headers.
    /// Converts &lt;br&gt; tags to real newlines (\n) in DialogueBox so dialogue line breaks
    /// survive Team Cherry's tag stripping.
    /// Reveals dialogue line-by-line, and on each line from right to left (top-right towards left).
    /// </summary>
    internal static class RtlPatch
    {
        private static readonly FieldInfo DialogueBoxTextMeshField =
            AccessTools.Field(typeof(DialogueBox), "textMesh");

        private static readonly FieldInfo IsPrintingTextField =
            AccessTools.Field(typeof(DialogueBox), "isPrintingText");

        private static readonly FieldInfo CurrentRevealSpeedField =
            AccessTools.Field(typeof(DialogueBox), "currentRevealSpeed");

        internal static TMProOld.TextAlignmentOptions ToRightAlignment(TMProOld.TextAlignmentOptions opt)
        {
            return opt switch
            {
                TMProOld.TextAlignmentOptions.TopLeft => TMProOld.TextAlignmentOptions.TopRight,
                TMProOld.TextAlignmentOptions.TopJustified => TMProOld.TextAlignmentOptions.TopRight,
                TMProOld.TextAlignmentOptions.Left => TMProOld.TextAlignmentOptions.Right,
                TMProOld.TextAlignmentOptions.Justified => TMProOld.TextAlignmentOptions.Right,
                TMProOld.TextAlignmentOptions.BottomLeft => TMProOld.TextAlignmentOptions.BottomRight,
                TMProOld.TextAlignmentOptions.BottomJustified => TMProOld.TextAlignmentOptions.BottomRight,
                TMProOld.TextAlignmentOptions.BaselineLeft => TMProOld.TextAlignmentOptions.BaselineRight,
                TMProOld.TextAlignmentOptions.BaselineJustified => TMProOld.TextAlignmentOptions.BaselineRight,
                TMProOld.TextAlignmentOptions.MidlineLeft => TMProOld.TextAlignmentOptions.MidlineRight,
                TMProOld.TextAlignmentOptions.MidlineJustified => TMProOld.TextAlignmentOptions.MidlineRight,
                TMProOld.TextAlignmentOptions.CaplineLeft => TMProOld.TextAlignmentOptions.CaplineRight,
                TMProOld.TextAlignmentOptions.CaplineJustified => TMProOld.TextAlignmentOptions.CaplineRight,
                _ => opt // Center, Top, Bottom, Midline, Baseline, etc. stay centered!
            };
        }

        /// <summary>
        /// DialogueBox strips all &lt;...&gt; tags except &lt;page&gt; and &lt;hpage&gt;.
        /// Convert &lt;br&gt; and &lt;br/&gt; to \n before stripping so line breaks are preserved into DialogueLine.Text.
        /// </summary>
        internal static void DialogueBoxParseTextPrefix(ref string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (text.Contains("<br") || text.Contains("&lt;br"))
            {
                text = text.Replace("<br/>", "\n")
                           .Replace("<br />", "\n")
                           .Replace("<br>", "\n")
                           .Replace("&lt;br/&gt;", "\n")
                           .Replace("&lt;br /&gt;", "\n")
                           .Replace("&lt;br&gt;", "\n");
            }
            if (text.Contains("<align=") || text.Contains("</align>"))
            {
                text = text.Replace("<align=right>", "")
                           .Replace("<align=left>", "")
                           .Replace("</align>", "");
            }
        }

        /// <summary>
        /// When DialogueBox opens or starts a conversation, ShowShared sets textMesh.alignment = displayOptions.Alignment
        /// (which defaults to TopLeft). If the dialogue text contains Persian, force alignment to TopRight.
        /// </summary>
        internal static void DialogueBoxShowSharedPostfix(DialogueBox __instance, string text)
        {
            if (__instance == null || string.IsNullOrEmpty(text)) return;

            if (PersianText.ContainsPersian(text) || PersianText.HasPresentationForms(text))
            {
                var tm = DialogueBoxTextMeshField?.GetValue(__instance) as TMProOld.TextMeshPro;
                if (tm != null)
                {
                    tm.alignment = TMProOld.TextAlignmentOptions.TopRight;
                }
            }
        }

        private static TMProOld.TMP_FontAsset _persianTmProOldFont;

        /// <summary>
        /// Finds the TMProOld Persian font asset in memory (loaded by CustomTranslation/CustomFont).
        /// </summary>
        internal static TMProOld.TMP_FontAsset GetPersianTmProOldFont()
        {
            if (_persianTmProOldFont != null) return _persianTmProOldFont;

            try
            {
                var assets = Resources.FindObjectsOfTypeAll<TMProOld.TMP_FontAsset>();
                foreach (var asset in assets)
                {
                    if (asset == null) continue;
                    var dict = asset.characterDictionary;
                    if (dict != null && (dict.ContainsKey(0xFE91) || dict.ContainsKey(0x0628) || dict.ContainsKey(0xFB8A)))
                    {
                        _persianTmProOldFont = asset;
                        Plugin.Log.LogInfo($"Found TMProOld Persian font asset: {asset.name}");
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Failed to search TMProOld font assets: {e.Message}");
            }

            return _persianTmProOldFont;
        }

        /// <summary>
        /// Ensures TMProOld components and TMP_Settings have the Persian font registered as fallback.
        /// </summary>
        internal static void EnsureTmProOldFallback(TMProOld.TMP_Text textComponent)
        {
            var persianFont = GetPersianTmProOldFont();
            if (persianFont == null) return;

            try
            {
                var fallbacks = TMProOld.TMP_Settings.fallbackFontAssets;
                if (fallbacks != null && !fallbacks.Contains(persianFont))
                {
                    fallbacks.Add(persianFont);
                }
            }
            catch { }

            try
            {
                if (textComponent != null && textComponent.font != null && textComponent.font != persianFont)
                {
                    if (textComponent.font.fallbackFontAssets == null)
                    {
                        textComponent.font.fallbackFontAssets = new List<TMProOld.TMP_FontAsset>();
                    }
                    if (!textComponent.font.fallbackFontAssets.Contains(persianFont))
                    {
                        textComponent.font.fallbackFontAssets.Add(persianFont);
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Intercept text assignments on TMProOld text components.
        /// </summary>
        internal static void TmProOldSetTextPrefix(TMProOld.TMP_Text __instance, ref string value)
        {
            if (__instance == null || string.IsNullOrEmpty(value)) return;

            if (PersianText.ContainsPersian(value) || PersianText.HasPresentationForms(value))
            {
                EnsureTmProOldFallback(__instance);

                // PromptMarker overhead prompt (talk, inspect, etc.) must ALWAYS remain Center aligned between the ornamental flourishes!
                if (__instance.GetComponentInParent<PromptMarker>() != null)
                {
                    __instance.alignment = TMProOld.TextAlignmentOptions.Center;
                    return;
                }

                // Only convert Left alignment to Right for multi-line paragraphs!
                // Single-line text (prompts, buttons, inline labels) retains original alignment.
                if (value.Contains('\n') || value.Contains("<br>"))
                {
                    __instance.alignment = ToRightAlignment(__instance.alignment);
                }
            }
        }

        /// <summary>
        /// Intercept SetText(string) on TMProOld text components.
        /// </summary>
        internal static void TmProOldSetTextStringPrefix(TMProOld.TMP_Text __instance, ref string text)
        {
            if (__instance == null || string.IsNullOrEmpty(text)) return;

            if (PersianText.ContainsPersian(text) || PersianText.HasPresentationForms(text))
            {
                EnsureTmProOldFallback(__instance);

                if (__instance.GetComponentInParent<PromptMarker>() != null)
                {
                    __instance.alignment = TMProOld.TextAlignmentOptions.Center;
                    return;
                }

                if (text.Contains('\n') || text.Contains("<br>"))
                {
                    __instance.alignment = ToRightAlignment(__instance.alignment);
                }
            }
        }

        /// <summary>
        /// Intercept alignment setter on TMProOld text components.
        /// If the component currently holds Persian text, map Left alignments to Right only for multi-line text.
        /// </summary>
        internal static void TmProOldSetAlignmentPrefix(TMProOld.TMP_Text __instance, ref TMProOld.TextAlignmentOptions value)
        {
            if (__instance == null) return;

            if (__instance.GetComponentInParent<PromptMarker>() != null)
            {
                value = TMProOld.TextAlignmentOptions.Center;
                return;
            }

            string txt = __instance.text;
            if (string.IsNullOrEmpty(txt)) return;

            if ((PersianText.ContainsPersian(txt) || PersianText.HasPresentationForms(txt)) &&
                (txt.Contains('\n') || txt.Contains("<br>")))
            {
                value = ToRightAlignment(value);
            }
        }

        /// <summary>
        /// FixVerticalAlign sets lineSpacing = -0.33f on UnityEngine.UI.Text for English font baseline tweaks.
        /// On Persian text (especially multi-line descriptions), negative line spacing crushes lines together.
        /// Ensure lineSpacing stays positive (1.05f) and prevent adding '\n'.
        /// </summary>
        internal static bool FixVerticalAlignPrefix(FixVerticalAlign __instance)
        {
            if (__instance == null) return true;
            var uiText = __instance.GetComponent<UnityEngine.UI.Text>();
            if (uiText == null || string.IsNullOrEmpty(uiText.text)) return true;

            if (PersianText.ContainsPersian(uiText.text) || PersianText.HasPresentationForms(uiText.text))
            {
                uiText.lineSpacing = 1.05f;
                return false; // Skip original AlignText that sets lineSpacing = -0.33f
            }
            return true;
        }

        internal static bool FixVerticalAlignKeymapPrefix(FixVerticalAlign __instance)
        {
            if (__instance == null) return true;
            var uiText = __instance.GetComponent<UnityEngine.UI.Text>();
            if (uiText == null || string.IsNullOrEmpty(uiText.text)) return true;

            if (PersianText.ContainsPersian(uiText.text) || PersianText.HasPresentationForms(uiText.text))
            {
                uiText.lineSpacing = 1.05f;
                return false;
            }
            return true;
        }

        // --- RTL Typewriter Animation for DialogueBox ---

        private static bool IsLtr(char c)
        {
            if (c >= 'A' && c <= 'Z') return true;
            if (c >= 'a' && c <= 'z') return true;
            if (c >= '0' && c <= '9') return true;
            if (c >= '\u06F0' && c <= '\u06F9') return true;
            if (c >= '\u0660' && c <= '\u0669') return true;
            return false;
        }

        private static void AddLineRevealOrder(TMProOld.TMP_TextInfo textInfo, int start, int end, List<int> order)
        {
            int i = end;
            while (i >= start)
            {
                char c = textInfo.characterInfo[i].character;
                if (IsLtr(c))
                {
                    int ltrEnd = i;
                    int ltrStart = i;
                    while (ltrStart - 1 >= start && IsLtr(textInfo.characterInfo[ltrStart - 1].character))
                    {
                        ltrStart--;
                    }
                    for (int k = ltrStart; k <= ltrEnd; k++)
                    {
                        if (textInfo.characterInfo[k].isVisible)
                            order.Add(k);
                    }
                    i = ltrStart - 1;
                }
                else
                {
                    if (textInfo.characterInfo[i].isVisible)
                        order.Add(i);
                    i--;
                }
            }
        }

        internal static bool DialogueBoxPrintTextPrefix(
            DialogueBox __instance,
            TMProOld.TMP_TextInfo textInfo,
            int pageIndex,
            ref IEnumerator __result)
        {
            if (__instance == null || textInfo == null) return true;

            var tm = DialogueBoxTextMeshField?.GetValue(__instance) as TMProOld.TextMeshPro;
            if (tm == null) return true;

            string txt = tm.text;
            if (string.IsNullOrEmpty(txt) || (!PersianText.ContainsPersian(txt) && !PersianText.HasPresentationForms(txt)))
            {
                return true; // Not Persian, let game handle it normally
            }

            __result = RtlPrintTextCoroutine(__instance, tm, textInfo, pageIndex);
            return false; // Skip original method
        }

        private static IEnumerator RtlPrintTextCoroutine(
            DialogueBox box,
            TMProOld.TextMeshPro textMesh,
            TMProOld.TMP_TextInfo textInfo,
            int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= textInfo.pageInfo.Length)
                yield break;

            var pageInfo = textInfo.pageInfo[pageIndex];

            IsPrintingTextField?.SetValue(box, true);

            // 1. Render all characters into the mesh so we have valid vertices
            textMesh.maxVisibleCharacters = int.MaxValue;
            textMesh.ForceMeshUpdate();

            // Refresh textInfo reference after ForceMeshUpdate
            textInfo = textMesh.textInfo;

            // 2. Hide all characters on this page by setting vertex alpha to 0
            for (int i = pageInfo.firstCharacterIndex; i <= pageInfo.lastCharacterIndex && i < textInfo.characterCount; i++)
            {
                if (!textInfo.characterInfo[i].isVisible) continue;
                int matIndex = textInfo.characterInfo[i].materialReferenceIndex;
                int vIndex = textInfo.characterInfo[i].vertexIndex;
                if (matIndex >= textInfo.meshInfo.Length) continue;

                var colors = textInfo.meshInfo[matIndex].colors32;
                if (colors == null || vIndex + 3 >= colors.Length) continue;

                colors[vIndex + 0].a = 0;
                colors[vIndex + 1].a = 0;
                colors[vIndex + 2].a = 0;
                colors[vIndex + 3].a = 0;
            }
            textMesh.UpdateVertexData(TMProOld.TMP_VertexDataUpdateFlags.Colors32);

            // 3. Build RTL reveal order line-by-line (each line from right to left)
            List<int> revealOrder = new List<int>();
            for (int l = 0; l < textInfo.lineCount; l++)
            {
                var line = textInfo.lineInfo[l];
                int start = Math.Max(line.firstCharacterIndex, pageInfo.firstCharacterIndex);
                int end = Math.Min(line.lastCharacterIndex, pageInfo.lastCharacterIndex);
                if (start > end) continue;

                // Ensure line belongs to this page
                if (textInfo.characterInfo[start].pageNumber + 1 != textMesh.pageToDisplay) continue;

                AddLineRevealOrder(textInfo, start, end, revealOrder);
            }

            // 4. Reveal characters progressively according to currentRevealSpeed
            if (revealOrder.Count > 0 && !CheatManager.IsTextPrintSkipEnabled)
            {
                float progress = 0f;
                int revealed = 0;

                while (revealed < revealOrder.Count)
                {
                    yield return null;

                    float speed = 30f; // fallback
                    if (CurrentRevealSpeedField != null)
                    {
                        speed = (float)CurrentRevealSpeedField.GetValue(box);
                    }

                    progress += speed * Time.deltaTime;
                    int target = Mathf.Min(Mathf.RoundToInt(progress), revealOrder.Count);

                    while (revealed < target)
                    {
                        int charIdx = revealOrder[revealed];
                        int matIndex = textInfo.characterInfo[charIdx].materialReferenceIndex;
                        int vIndex = textInfo.characterInfo[charIdx].vertexIndex;

                        if (matIndex < textInfo.meshInfo.Length)
                        {
                            var colors = textInfo.meshInfo[matIndex].colors32;
                            if (colors != null && vIndex + 3 < colors.Length)
                            {
                                colors[vIndex + 0].a = 255;
                                colors[vIndex + 1].a = 255;
                                colors[vIndex + 2].a = 255;
                                colors[vIndex + 3].a = 255;
                            }
                        }
                        revealed++;
                    }

                    textMesh.UpdateVertexData(TMProOld.TMP_VertexDataUpdateFlags.Colors32);
                }
            }

            // 5. Ensure all characters on the page are fully visible at the end
            for (int i = pageInfo.firstCharacterIndex; i <= pageInfo.lastCharacterIndex && i < textInfo.characterCount; i++)
            {
                if (!textInfo.characterInfo[i].isVisible) continue;
                int matIndex = textInfo.characterInfo[i].materialReferenceIndex;
                int vIndex = textInfo.characterInfo[i].vertexIndex;
                if (matIndex >= textInfo.meshInfo.Length) continue;

                var colors = textInfo.meshInfo[matIndex].colors32;
                if (colors == null || vIndex + 3 >= colors.Length) continue;

                colors[vIndex + 0].a = 255;
                colors[vIndex + 1].a = 255;
                colors[vIndex + 2].a = 255;
                colors[vIndex + 3].a = 255;
            }
            textMesh.UpdateVertexData(TMProOld.TMP_VertexDataUpdateFlags.Colors32);

            IsPrintingTextField?.SetValue(box, false);
        }
    }
}
