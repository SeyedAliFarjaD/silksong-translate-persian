using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using Newtonsoft.Json;

namespace SilksongPersian
{
    /// <summary>
    /// Reads the Persian sheet JSON shipped next to the plugin and rebuilds the game's own sheet
    /// XML so its existing reader does the parsing.
    ///
    /// The English sheet is always layered underneath the Persian one. That way a key we have not
    /// translated yet (or one the game keeps in a sheet whose English text lives elsewhere) still
    /// resolves to English instead of the raw key marker the game shows for a missing entry.
    /// </summary>
    internal static class TranslationStore
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Sheets =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        private static readonly Regex EntryRegex = new Regex(
            "<entry\\s+name=\"([^\"]*)\"\\s*>(.*?)</entry>",
            RegexOptions.Singleline | RegexOptions.Compiled);

        internal static int SheetCount => Sheets.Count;

        internal static int EntryCount
        {
            get
            {
                int total = 0;
                foreach (var sheet in Sheets.Values) total += sheet.Count;
                return total;
            }
        }

        internal static string Folder { get; private set; }

        internal static void Load()
        {
            Folder = Path.Combine(Paths.PluginPath, "SilksongPersian", "fa");
            Sheets.Clear();

            if (!Directory.Exists(Folder))
            {
                Plugin.Log.LogError($"Persian sheet folder not found: {Folder}");
                return;
            }

            foreach (string file in Directory.GetFiles(Folder, "*.json"))
            {
                string sheet = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var parsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                        File.ReadAllText(file, Encoding.UTF8));
                    if (parsed == null || parsed.Count == 0) continue;
                    Sheets[sheet] = parsed;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Could not read {Path.GetFileName(file)}: {e.Message}");
                }
            }

            Plugin.Log.LogInfo($"Persian sheets loaded: {SheetCount} sheet(s), {EntryCount} entries.");
        }

        internal static bool HasSheet(string sheetTitle) => Sheets.ContainsKey(sheetTitle);

        internal static int KeysFor(string sheetTitle) =>
            Sheets.TryGetValue(sheetTitle, out var s) ? s.Count : 0;

        /// <summary>
        /// Merge the English XML for a sheet with our Persian entries. English values are already
        /// XML-escaped by the game, so they are copied verbatim; Persian values are shaped and
        /// escaped here.
        /// </summary>
        internal static string BuildMergedSheetXml(string sheetTitle, string englishXml, out int persianKeys)
        {
            var merged = new Dictionary<string, string>(StringComparer.Ordinal);

            if (!string.IsNullOrEmpty(englishXml))
            {
                foreach (Match m in EntryRegex.Matches(englishXml))
                {
                    merged[m.Groups[1].Value] = m.Groups[2].Value;
                }
            }

            persianKeys = 0;
            if (Sheets.TryGetValue(sheetTitle, out var persian))
            {
                foreach (var pair in persian)
                {
                    // Shaping happens here rather than in a TMP hook: the game never routes its menu
                    // text through the TMP setters, and this layer sees every localised string.
                    merged[pair.Key] = Escape(PersianText.ShapeAndReorder(pair.Value));
                    persianKeys++;
                }
            }

            if (merged.Count == 0) return null;

            // Entries MUST be separated by whitespace. The game's reader calls
            // ReadElementContentAsString() (which lands on the node after </entry>) and then
            // ReadToFollowing("entry"), which skips whatever node it is already sitting on.
            // With no separator that is the very next <entry>, so every other key disappears.
            var sb = new StringBuilder(merged.Count * 64 + 32);
            sb.Append("<entries>\n");
            foreach (var pair in merged)
            {
                sb.Append("<entry name=\"").Append(Escape(pair.Key)).Append("\">");
                sb.Append(pair.Value);
                sb.Append("</entry>\n");
            }
            sb.Append("</entries>");
            return sb.ToString();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var sb = new StringBuilder(value.Length + 16);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'': sb.Append("&apos;"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
