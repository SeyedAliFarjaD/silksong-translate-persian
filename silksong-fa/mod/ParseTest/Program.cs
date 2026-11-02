using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace ParseTest
{
    /// <summary>
    /// Reproduces the game's own sheet parsing offline:
    ///   while (reader.ReadToFollowing("entry")) { MoveToFirstAttribute(); key = Value;
    ///           MoveToElement(); value = ReadElementContentAsString().Trim().UnescapeXml(); }
    /// It then checks that every key we ship survives that loop.
    /// </summary>
    internal static class Program
    {
        private static readonly Regex EntryRegex = new Regex(
            "<entry\\s+name=\"([^\"]*)\"\\s*>(.*?)</entry>",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static int Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            string baseDir = @"C:\Click - Copy\rea\silksong-fa";
            string enJson = File.ReadAllText(Path.Combine(baseDir, "text", "EN.json"), Encoding.UTF8);
            var english = JsonSerializer.Deserialize<Dictionary<string, string>>(enJson);

            // Focused dump for one sheet so the drop pattern is visible.
            Dump("General", english);

            string faDir = Path.Combine(baseDir, "work", "fa");
            int sheetsChecked = 0, lostKeys = 0, valueMismatch = 0;

            foreach (string faFile in Directory.GetFiles(faDir, "*.json"))
            {
                string sheet = Path.GetFileNameWithoutExtension(faFile);
                var persian = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllText(faFile, Encoding.UTF8));

                english.TryGetValue(sheet, out string englishXml);
                string merged = BuildMerged(englishXml, persian);

                var parsed = ParseLikeGame(merged);

                sheetsChecked++;
                var missing = new List<string>();
                foreach (var pair in persian)
                {
                    if (!parsed.ContainsKey(pair.Key)) { missing.Add(pair.Key); continue; }
                    if (parsed[pair.Key] != pair.Value) valueMismatch++;
                }

                if (missing.Count > 0)
                {
                    lostKeys += missing.Count;
                    Console.WriteLine($"LOST in '{sheet}': {missing.Count} key(s) -> {string.Join(", ", missing.GetRange(0, Math.Min(5, missing.Count)))}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"sheets checked : {sheetsChecked}");
            Console.WriteLine($"keys lost      : {lostKeys}");
            Console.WriteLine($"value mismatches: {valueMismatch}");
            Console.WriteLine(lostKeys == 0 && valueMismatch == 0 ? "ROUND TRIP OK" : "ROUND TRIP BROKEN");
            return 0;
        }

        private static void Dump(string sheet, Dictionary<string, string> english)
        {
            string baseDir = @"C:\Click - Copy\rea\silksong-fa";
            var persian = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(Path.Combine(baseDir, "work", "fa", sheet + ".json"), Encoding.UTF8));

            english.TryGetValue(sheet, out string englishXml);
            string merged = BuildMerged(englishXml, persian);

            int xmlEntries = Regex.Matches(merged, "<entry").Count;
            var parsed = ParseLikeGame(merged);

            Console.WriteLine($"=== {sheet} ===");
            Console.WriteLine($"xml <entry> tags : {xmlEntries}");
            Console.WriteLine($"parsed keys      : {parsed.Count}");
            Console.WriteLine($"persian keys     : {persian.Count}");
            Console.WriteLine("first 400 chars of XML:");
            Console.WriteLine(merged.Substring(0, Math.Min(400, merged.Length)));
            Console.WriteLine("parsed key order:");
            Console.WriteLine("  " + string.Join(", ", parsed.Keys));
            Console.WriteLine();
        }

        private static string BuildMerged(string englishXml, Dictionary<string, string> persian)
        {
            var merged = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(englishXml))
            {
                foreach (Match m in EntryRegex.Matches(englishXml))
                    merged[m.Groups[1].Value] = m.Groups[2].Value;
            }
            foreach (var pair in persian)
                merged[pair.Key] = Escape(pair.Value);

            var sb = new StringBuilder();
            sb.Append("<entries>\n");
            foreach (var pair in merged)
                sb.Append("<entry name=\"").Append(Escape(pair.Key)).Append("\">").Append(pair.Value).Append("</entry>\n");
            sb.Append("</entries>");
            return sb.ToString();
        }

        private static Dictionary<string, string> ParseLikeGame(string xml)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            using var reader = XmlReader.Create(new StringReader(xml));
            while (reader.ReadToFollowing("entry"))
            {
                reader.MoveToFirstAttribute();
                string key = reader.Value;
                reader.MoveToElement();
                string value = reader.ReadElementContentAsString().Trim();
                value = UnescapeXml(value);
                result[key] = value;
            }
            return result;
        }

        private static string UnescapeXml(string value) =>
            value.Replace("&apos;", "'").Replace("&quot;", "\"")
                 .Replace("&gt;", ">").Replace("&lt;", "<").Replace("&amp;", "&");

        private static string Escape(string value) =>
            value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                 .Replace("\"", "&quot;").Replace("'", "&apos;");
    }
}
