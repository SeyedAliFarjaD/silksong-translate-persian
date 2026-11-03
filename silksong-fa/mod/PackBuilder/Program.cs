using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace PackBuilder
{
    /// <summary>
    /// Builds the entry.json consumed by CustomTranslation, applying the Persian shaping and
    /// bidi reordering that the community mod does not perform (it was written for Hebrew and
    /// Chinese, neither of which needs Arabic joining).
    /// </summary>
    internal static class Program
    {
        private static int GetMaxLineLength(string sheet, string key)
        {
            if (key == "SAVE_WARNING")
            {
                return 0; // Keep exactly as 2 lines matching English original
            }

            // Shop and item descriptions in any sheet are displayed in narrow shop/inventory cards (~24 chars)
            if (key.EndsWith("_DESC") || key.Contains("_DESC_") || key.Contains("_ITEM_") || key.Contains("SHOP_") || key.Contains("_SHOP"))
            {
                if (sheet != "MainMenu" && sheet != "Achievements" && sheet != "General")
                {
                    return 24;
                }
            }

            switch (sheet)
            {
                // Narrow UI Cards, Tooltips, Inventories:
                case "UI":
                case "Tools":
                case "Shop":
                case "Prompts":
                case "Quests":
                case "Journal":
                    return 24;

                // Scenery inspection, tablets, lore, achievements, credits:
                case "Inspect":
                case "Lore":
                case "Achievements":
                case "Credits List":
                case "Error":
                    return 38;

                // NPC Dialogue sheets (wide dialogue box):
                case "Belltown":
                case "Bonebottom":
                case "Caravan":
                case "City":
                case "Coral":
                case "Crawl":
                case "Dust":
                case "Enclave":
                case "Forge":
                case "Greymoor":
                case "Peak":
                case "Pilgrims":
                case "Shellwood":
                case "Song":
                case "Under":
                case "Wanderers":
                case "Weave":
                case "Wilds":
                    return 42;

                // Menus, Options descriptions:
                case "MainMenu":
                    return 55;

                case "General":
                    return 0;

                default:
                    return 28;
            }
        }

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            string baseDir = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "work", "fa"))
                ? AppContext.BaseDirectory
                : Directory.Exists(Path.Combine(Environment.CurrentDirectory, "silksong-fa", "work", "fa"))
                    ? Path.Combine(Environment.CurrentDirectory, "silksong-fa")
                    : Directory.Exists(Path.Combine(Environment.CurrentDirectory, "work", "fa"))
                        ? Environment.CurrentDirectory
                        : @"C:\Click - Copy\rea\silksong-fa";
            string faDir = Path.Combine(baseDir, "work", "fa");
            string outDir = args.Length > 0
                ? args[0]
                : @"C:\GOG Games\Hollow Knight Silksong\BepInEx\plugins\CarrieForle-CustomTranslation\translation\fa";

            Directory.CreateDirectory(outDir);

            var sheets = new SortedDictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            int shapedValues = 0;

            foreach (string file in Directory.GetFiles(faDir, "*.json"))
            {
                string sheet = Path.GetFileNameWithoutExtension(file);
                var source = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllText(file, Encoding.UTF8));

                var shaped = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var pair in source)
                {
                    int maxLineLength = GetMaxLineLength(sheet, pair.Key);
                    string value = SilksongPersian.PersianText.ShapeAndReorder(pair.Value, maxLineLength);
                    if (!ReferenceEquals(value, pair.Value)) shapedValues++;
                    shaped[pair.Key] = value;
                }
                sheets[sheet] = shaped;
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };

            File.WriteAllText(
                Path.Combine(outDir, "entry.json"),
                JsonSerializer.Serialize(sheets, options),
                new UTF8Encoding(false));

            var metadata = new Dictionary<string, object>
            {
                ["Language"] = "FA",
                ["FallbackLanguage"] = "EN",
                ["TextFontScale"] = 1.0,
                ["TitleFontScale"] = 1.0,
                ["UseFontAsFallBack"] = true,
            };
            File.WriteAllText(
                Path.Combine(outDir, "metadata.json"),
                JsonSerializer.Serialize(metadata, options),
                new UTF8Encoding(false));

            int keys = 0;
            foreach (var sheet in sheets.Values) keys += sheet.Count;

            Console.WriteLine($"sheets : {sheets.Count}");
            Console.WriteLine($"keys   : {keys}");
            Console.WriteLine($"shaped : {shapedValues} value(s) changed by shaping");
            Console.WriteLine($"output : {outDir}");
            return 0;
        }
    }
}
