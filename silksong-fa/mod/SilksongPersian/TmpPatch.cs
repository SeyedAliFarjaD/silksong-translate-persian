using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace SilksongPersian
{
    /// <summary>
    /// TextMeshPro performs neither Arabic shaping nor bidi reordering, and its own
    /// isRightToLeftText flag mirrors the result instead of ordering it.
    ///
    /// The shipped Persian text is already shaped and reordered by the localisation layer, so the
    /// remaining job here is the font: presentation-form glyphs only exist in a Persian typeface,
    /// and the game's own fonts render them as empty boxes. A TMP font asset is therefore built at
    /// runtime and registered as a global fallback.
    /// </summary>
    internal static class TmpPatch
    {
        private const string FontFamily = "Vazirmatn";
        private const string FontFile = "Vazirmatn-Regular.ttf";
        private const string RegularStyle = "Regular";
        private const int FontSamplingSize = 90;
        private const int AtlasPadding = 9;
        private const int AtlasWidth = 1024;
        private const int AtlasHeight = 1024;

        private const uint FR_PRIVATE = 0x10;

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int AddFontResourceEx(string fileName, uint flags, IntPtr reserved);

        private static TMP_FontAsset _persianFont;
        private static bool _fontAttempted;
        private static float _lastAttempt;
        private const float RetrySeconds = 1f;

        /// <summary>
        /// Try to build the Persian font. Safe to call repeatedly: it returns false while the
        /// renderer is not ready yet (TMP resolves its SDF shader with Shader.Find, which returns
        /// null before the game's own font bundles are loaded) and true once it succeeds.
        /// </summary>
        internal static bool TryEnsureFont()
        {
            if (_persianFont != null) return true;
            if (_fontAttempted && _lastAttempt > 0 && Time.realtimeSinceStartup - _lastAttempt < RetrySeconds)
                return false;

            _lastAttempt = Time.realtimeSinceStartup;
            _fontAttempted = true;

            try
            {
                string bundled = ResolveBundledFont();
                if (bundled == null)
                {
                    Plugin.Log.LogError("Bundled Persian font not found next to the plugin.");
                    return false;
                }

                _persianFont = CreateManually(bundled) ?? CreateFromPath(bundled, "bundled file");

                if (_persianFont == null) return false;

                _persianFont.name = "Vazirmatn Persian (runtime)";
                RegisterFallback(_persianFont);
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Persian font attempt failed: {e.GetType().Name}");
                return false;
            }
        }

        /// <summary>Legacy single-shot entry point kept for the diagnostics path.</summary>
        internal static void EnsureFont() => TryEnsureFont();

        /// <summary>
        /// Attach the Persian face to every font asset currently in memory and report how many
        /// took it. Repeatable: the game loads its fonts progressively, so early calls may find
        /// nothing. Returns the number of assets newly attached in this pass.
        /// </summary>
        internal static int TryAttachFallback()
        {
            if (_persianFont == null) return 0;

            // Font assets are read off the live text components. A global asset search returns
            // nothing here because the game's fonts are loaded from bundles, so the components
            // that are actually rendering are the reliable source.
            var fonts = new List<TMP_FontAsset>();
            int textComponents = 0;

            try
            {
                foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
                {
                    if (text == null) continue;
                    textComponents++;
                    var font = text.font;
                    if (font != null && font != _persianFont && !fonts.Contains(font)) fonts.Add(font);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not enumerate text components: {e.Message}");
            }

            try
            {
                foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                {
                    if (font != null && font != _persianFont && !fonts.Contains(font)) fonts.Add(font);
                }
            }
            catch
            {
                // Non-fatal: the component scan above is the primary source.
            }

            if (!_discoveryLogged && textComponents > 0)
            {
                _discoveryLogged = true;
                Plugin.Log.LogInfo(
                    $"TMP discovery: {textComponents} text component(s), {fonts.Count} distinct font asset(s).");
            }

            int attached = 0;
            foreach (var candidate in fonts)
            {
                try
                {
                    var table = candidate.fallbackFontAssetTable;
                    if (table != null && !table.Contains(_persianFont))
                    {
                        table.Add(_persianFont);
                        attached++;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"fallbackFontAssetTable failed on {candidate.name}: {e.Message}");
                }

                attached += AttachLanguageFallback(candidate);
            }

            if (attached > 0)
            {
                _attachedTotal += attached;
                Plugin.Log.LogInfo($"Persian fallback attached ({_attachedTotal} total).");
            }
            return attached;
        }

        private static bool _discoveryLogged;

        /// <summary>
        /// Team Cherry extended TMP_FontAsset with a per-language fallback field. Populate it for
        /// Persian when its shape allows, since that may be the lookup their text path uses.
        /// </summary>
        private static int AttachLanguageFallback(TMP_FontAsset candidate)
        {
            const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance;

            try
            {
                var type = candidate.GetType();
                object holder = type.GetProperty("languageFallbacks", Flags)?.GetValue(candidate)
                                ?? type.GetField("languageFallbacks", Flags)?.GetValue(candidate);
                if (holder == null) return 0;

                if (!_languageFallbacksLogged)
                {
                    _languageFallbacksLogged = true;
                    Plugin.Log.LogInfo($"languageFallbacks type: {holder.GetType().FullName}");
                }

                // Dictionary<string, List<TMP_FontAsset>> style container.
                var asDictionary = holder as System.Collections.IDictionary;
                if (asDictionary != null)
                {
                    if (asDictionary.Contains(LocalizationPatch.PersianCode)) return 0;
                    var list = new List<TMP_FontAsset> { _persianFont };
                    asDictionary[LocalizationPatch.PersianCode] = list;
                    return 1;
                }

                var asList = holder as System.Collections.IList;
                if (asList != null)
                {
                    if (asList.Count > 0) return 0;
                    asList.Add(new KeyValuePair<string, List<TMP_FontAsset>>(
                        LocalizationPatch.PersianCode, new List<TMP_FontAsset> { _persianFont }));
                    return 1;
                }
            }
            catch (Exception e)
            {
                if (!_languageFallbacksLogged)
                {
                    _languageFallbacksLogged = true;
                    Plugin.Log.LogWarning($"languageFallbacks not writable: {e.Message}");
                }
            }
            return 0;
        }

        private static int _attachedTotal;
        private static bool _languageFallbacksLogged;

        /// <summary>
        /// Attach the Persian face as a fallback on every font asset already in memory. This avoids
        /// depending on TMP_Settings, which this build does not ship.
        /// </summary>
        private static void RegisterFallback(TMP_FontAsset font)
        {
            _persianFont = font;
            TryAttachFallback();
        }

        private static string ResolveBundledFont()
        {
            try
            {
                string path = Path.Combine(Paths.PluginPath, "SilksongPersian", "fonts", FontFile);
                path = Path.GetFullPath(path);
                return File.Exists(path) ? path : null;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not resolve the bundled font: {e.Message}");
                return null;
            }
        }

        internal static string ResolveFontPathForProbe() => ResolveBundledFont();

        /// <summary>How many TMP font assets the game currently has in memory (readiness signal).</summary>
        internal static int LoadedFontAssetCount()
        {
            try
            {
                return Resources.FindObjectsOfTypeAll<TMP_FontAsset>().Length;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Build the asset but postpone it until the renderer is ready. TMP's factory resolves the
        /// SDF shader through Shader.Find, which fails before the game's font bundles are loaded.
        /// </summary>
        internal static void EnsureFontDeferred()
        {
            if (_fontAttempted) return;
            if (LoadedFontAssetCount() == 0) return;
            EnsureFont();
        }

        /// <summary>Unity's font engine is happiest with a short ASCII path, so keep a copy there.</summary>
        private static string CopyToTemp(string source)
        {
            if (source == null) return null;
            try
            {
                string dir = Path.Combine(Application.temporaryCachePath, "SilksongPersian");
                Directory.CreateDirectory(dir);
                string target = Path.Combine(dir, FontFile);
                File.Copy(source, target, true);
                Plugin.Log.LogInfo($"Font copied to {target}");
                return target;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not copy the font to temp: {e.Message}");
                return null;
            }
        }

        private static TMP_FontAsset CreateFromPath(string path, string label)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            try
            {
                var asset = TMP_FontAsset.CreateFontAsset(
                    path, 0, FontSamplingSize, AtlasPadding,
                    GlyphRenderMode.SDFAA, AtlasWidth, AtlasHeight);

                if (asset == null)
                {
                    Plugin.Log.LogWarning($"CreateFontAsset returned null for the {label}.");
                    return null;
                }

                asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                Plugin.Log.LogInfo($"Persian font built from the {label}.");
                return asset;
            }
            catch (Exception e)
            {
                // Full trace: TMP throws a bare NRE and the frame names the missing object.
                Plugin.Log.LogWarning($"CreateFontAsset threw for the {label}: {e}");
                return null;
            }
        }

        /// <summary>
        /// Rebuild what TMP's factory does, but reuse a material that already works in this build.
        /// This bypasses whatever the factory dereferences when it fails.
        /// </summary>
        private static TMP_FontAsset CreateManually(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            try
            {
                if (FontEngine.LoadFontFace(path, FontSamplingSize, 0) != FontEngineError.Success)
                {
                    Plugin.Log.LogWarning("Manual build: FontEngine could not load the face.");
                    return null;
                }

                // This build ships no TMP Settings asset, so TMP_Settings.clearDynamicDataOnBuild
                // throws inside the factory. Everything it needs is built here instead.
                Shader shader = Shader.Find("TextMeshPro/Mobile/Distance Field")
                                ?? Shader.Find("TextMeshPro/Distance Field");
                if (shader == null)
                {
                    Plugin.Log.LogWarning("Manual build: the TMP SDF shader was not found.");
                    return null;
                }

                var asset = ScriptableObject.CreateInstance<TMP_FontAsset>();
                var texture = new Texture2D(1, 1, TextureFormat.Alpha8, false);
                var material = new Material(shader) { name = "Vazirmatn Persian (runtime)" };

                Set(asset, "m_Version", "1.1.0");
                Set(asset, "faceInfo", FontEngine.GetFaceInfo());
                Set(asset, "atlasPopulationMode", AtlasPopulationMode.Dynamic);
                Set(asset, "atlasWidth", AtlasWidth);
                Set(asset, "atlasHeight", AtlasHeight);
                Set(asset, "atlasPadding", AtlasPadding);
                Set(asset, "atlasRenderMode", GlyphRenderMode.SDFAA);
                Set(asset, "atlasTextures", new[] { texture });
                Set(asset, "isMultiAtlasTexturesEnabled", true);
                Set(asset, "material", material);
                Set(asset, "m_SourceFontFilePath", path);
                Set(asset, "freeGlyphRects", new List<GlyphRect>
                {
                    new GlyphRect(0, 0, AtlasWidth - 1, AtlasHeight - 1)
                });
                Set(asset, "usedGlyphRects", new List<GlyphRect>());

                material.SetTexture(ShaderUtilities.ID_MainTex, texture);
                material.SetFloat(ShaderUtilities.ID_TextureWidth, AtlasWidth);
                material.SetFloat(ShaderUtilities.ID_TextureHeight, AtlasHeight);
                material.SetFloat(ShaderUtilities.ID_GradientScale, AtlasPadding + 1);

                // May itself touch the missing settings asset; the font is usable either way.
                try
                {
                    Invoke(asset, "ReadFontAssetDefinition");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"ReadFontAssetDefinition failed: {e.GetType().Name}");
                }

                Plugin.Log.LogInfo("Persian font built manually (no TMP Settings dependency).");
                return asset;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Manual font build failed: {e}");
                return null;
            }
        }

        private static Material FindWorkingMaterial()
        {
            try
            {
                var assets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                foreach (var candidate in assets)
                {
                    if (candidate != null && candidate.material != null) return candidate.material;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not inspect existing font assets: {e.Message}");
            }
            return null;
        }

        private static void Set(object target, string member, object value)
        {
            const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance;

            var type = target.GetType();
            var property = type.GetProperty(member, Flags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value);
                return;
            }

            var field = type.GetField(member, Flags);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            Plugin.Log.LogWarning($"TMP_FontAsset has no settable member '{member}'.");
        }

        private static void Invoke(object target, string method)
        {
            const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance;

            var info = target.GetType().GetMethod(method, Flags);
            if (info == null)
            {
                Plugin.Log.LogWarning($"TMP_FontAsset has no method '{method}'.");
                return;
            }
            info.Invoke(target, null);
        }

        /// <summary>
        /// Register the TTF privately for this process, then let TMP resolve it by family name.
        /// This is the path that works when the font is not installed system-wide.
        /// </summary>
        private static TMP_FontAsset CreateFromPrivateOsFont(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            try
            {
                int added = AddFontResourceEx(path, FR_PRIVATE, IntPtr.Zero);
                Plugin.Log.LogInfo($"AddFontResourceEx returned {added} for the private font.");

                var asset = TMP_FontAsset.CreateFontAsset(FontFamily, RegularStyle, FontSamplingSize);
                if (asset == null)
                {
                    Plugin.Log.LogWarning("Family-name font creation failed after private registration.");
                    return null;
                }

                asset.atlasPopulationMode = AtlasPopulationMode.DynamicOS;
                Plugin.Log.LogInfo("Persian font built from the privately registered face.");
                return asset;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Private font registration failed: {e.Message}");
                return null;
            }
        }

        internal static TextAlignmentOptions ToRightAlignment(TextAlignmentOptions opt)
        {
            return opt switch
            {
                TextAlignmentOptions.TopLeft => TextAlignmentOptions.TopRight,
                TextAlignmentOptions.TopJustified => TextAlignmentOptions.TopRight,
                TextAlignmentOptions.TopFlush => TextAlignmentOptions.TopRight,
                TextAlignmentOptions.TopGeoAligned => TextAlignmentOptions.TopRight,
                TextAlignmentOptions.Left => TextAlignmentOptions.Right,
                TextAlignmentOptions.Justified => TextAlignmentOptions.Right,
                TextAlignmentOptions.Flush => TextAlignmentOptions.Right,
                TextAlignmentOptions.BottomLeft => TextAlignmentOptions.BottomRight,
                TextAlignmentOptions.BottomJustified => TextAlignmentOptions.BottomRight,
                TextAlignmentOptions.BottomFlush => TextAlignmentOptions.BottomRight,
                TextAlignmentOptions.BottomGeoAligned => TextAlignmentOptions.BottomRight,
                TextAlignmentOptions.BaselineLeft => TextAlignmentOptions.BaselineRight,
                TextAlignmentOptions.BaselineJustified => TextAlignmentOptions.BaselineRight,
                TextAlignmentOptions.BaselineFlush => TextAlignmentOptions.BaselineRight,
                TextAlignmentOptions.BaselineGeoAligned => TextAlignmentOptions.BaselineRight,
                TextAlignmentOptions.MidlineLeft => TextAlignmentOptions.MidlineRight,
                TextAlignmentOptions.MidlineJustified => TextAlignmentOptions.MidlineRight,
                TextAlignmentOptions.MidlineFlush => TextAlignmentOptions.MidlineRight,
                TextAlignmentOptions.MidlineGeoAligned => TextAlignmentOptions.MidlineRight,
                TextAlignmentOptions.CaplineLeft => TextAlignmentOptions.CaplineRight,
                TextAlignmentOptions.CaplineJustified => TextAlignmentOptions.CaplineRight,
                TextAlignmentOptions.CaplineFlush => TextAlignmentOptions.CaplineRight,
                TextAlignmentOptions.CaplineGeoAligned => TextAlignmentOptions.CaplineRight,
                _ => opt // Center, Top, Bottom, etc. stay centered!
            };
        }

        private static void Fix(TMP_Text instance, ref string value)
        {
            if (instance == null || string.IsNullOrEmpty(value)) return;

            if (instance.isRightToLeftText) instance.isRightToLeftText = false;

            // Values from sheets are already shaped; this only catches text set from elsewhere.
            string shaped = PersianText.ShapeAndReorder(value);
            if (!ReferenceEquals(shaped, value)) value = shaped;

            if (PersianText.ContainsPersian(value) || PersianText.HasPresentationForms(value))
            {
                // Only convert Left alignment to Right for multi-line paragraphs (cards, tooltips, dialogue).
                // Single-line text (buttons, prompts, inline labels) retains original layout alignment.
                if (value.Contains('\n') || value.Contains("<br>"))
                {
                    instance.alignment = ToRightAlignment(instance.alignment);
                }
            }
        }

        internal static void SetAlignmentPrefix(TMP_Text __instance, ref TextAlignmentOptions value)
        {
            if (__instance == null) return;
            string txt = __instance.text;
            if (string.IsNullOrEmpty(txt)) return;

            if ((PersianText.ContainsPersian(txt) || PersianText.HasPresentationForms(txt)) &&
                (txt.Contains('\n') || txt.Contains("<br>")))
            {
                value = ToRightAlignment(value);
            }
        }

        // Harmony binds by parameter name, so these must match TMP's own names (sourceText).
        internal static void SetTextPrefix(TMP_Text __instance, ref string sourceText) => Fix(__instance, ref sourceText);

        internal static void SetTextBoolPrefix(TMP_Text __instance, ref string sourceText) => Fix(__instance, ref sourceText);

        internal static void UguiSetTextPrefix(TextMeshProUGUI __instance, ref string value) => Fix(__instance, ref value);

        internal static void TmpSetTextPrefix(TextMeshPro __instance, ref string value) => Fix(__instance, ref value);

        internal static void SetRightToLeftPrefix(ref bool value)
        {
            if (value) value = false;
        }
    }
}
