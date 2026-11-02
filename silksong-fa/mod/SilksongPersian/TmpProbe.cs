using System;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace SilksongPersian
{
    /// <summary>
    /// Reports the TMP runtime state that CreateFontAsset depends on. TMP's factory throws a bare
    /// NullReferenceException when one of these is missing, so each candidate is tested separately.
    /// </summary>
    internal static class TmpProbe
    {
        internal static void Report(string fontPath)
        {
            Try("TMP_Settings.instance", () => (TMP_Settings.instance == null).ToString());
            Try("clearDynamicDataOnBuild", () => TMP_Settings.clearDynamicDataOnBuild.ToString());
            Try("fallbackFontAssets", () => (TMP_Settings.fallbackFontAssets == null).ToString());
            Try("ShaderRef_MobileSDF", () => DescribeShaderRef("ShaderRef_MobileSDF"));
            Try("ShaderRef_Sprite", () => DescribeShaderRef("ShaderRef_Sprite"));
            Try("ShaderRef_MobileBitmap", () => DescribeShaderRef("ShaderRef_MobileBitmap"));
            Try("Shader.Find(mobile)", () => (Shader.Find("TextMeshPro/Mobile/Distance Field") != null).ToString());
            Try("Shader.Find(desktop)", () => (Shader.Find("TextMeshPro/Distance Field") != null).ToString());
            Try("ID_MainTex", () => ShaderUtilities.ID_MainTex.ToString());
            Try("ID_GradientScale", () => ShaderUtilities.ID_GradientScale.ToString());

            if (!string.IsNullOrEmpty(fontPath))
            {
                Try("LoadFontFace", () => FontEngine.LoadFontFace(fontPath, 90, 0).ToString());
                Try("GetFaceInfo", () =>
                {
                    var info = FontEngine.GetFaceInfo();
                    return $"family={info.familyName} style={info.styleName} pointSize={info.pointSize}";
                });
            }

            Try("existing TMP_FontAssets", () =>
            {
                var assets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                int usable = 0;
                foreach (var a in assets)
                {
                    if (a == null) continue;
                    usable++;
                    if (usable <= 6)
                        Plugin.Log.LogInfo($"    font: {a.name} mode={a.atlasPopulationMode} hasMaterial={a.material != null}");
                }
                return usable.ToString();
            });
        }

        private static string DescribeShaderRef(string member)
        {
            const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static;

            var property = typeof(ShaderUtilities).GetProperty(member, Flags);
            if (property == null) return "member not found";

            object value = property.GetValue(null);
            return value == null ? "NULL" : $"set ({(value as Shader)?.name})";
        }

        private static void Try(string label, Func<string> probe)
        {
            try
            {
                Plugin.Log.LogInfo($"PROBE {label}: {probe()}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"PROBE {label}: THREW {e.GetType().Name}: {e.Message}");
            }
        }
    }
}
