using System;
using System.Collections.Generic;
using System.Text;

namespace SilksongPersian
{
    /// <summary>
    /// Persian/Arabic text pipeline for TextMeshPro.
    ///
    /// TMP performs no Arabic shaping and no bidi reordering, so Persian arrives at the
    /// renderer as isolated glyphs in logical (right-to-left) order and displays as
    /// disconnected letters reading backwards. This class fixes that by:
    ///   1. protecting TMP rich-text tags and {n} placeholders as atomic tokens,
    ///   2. mapping letters to Arabic Presentation Forms-B by joining context,
    ///   3. resolving the lam-alef ligatures,
    ///   4. reordering to visual left-to-right order while keeping Latin/digit runs intact
    ///      and mirroring paired punctuation.
    ///
    /// The output is meant for a TMP component with isRightToLeft forced to false: doing the
    /// reordering ourselves is what prevents the mirrored/"flipped" text that flag causes.
    /// </summary>
    internal static class PersianText
    {
        // ---- joining classes -------------------------------------------------
        private const int NonJoining = 0;
        private const int RightJoining = 1;
        private const int DualJoining = 2;
        private const int Transparent = 3;

        private const char Zwnj = '\u200C';
        private const char Tatweel = '\u0640';

        // ---- presentation form tables ---------------------------------------
        // index 0 = isolated, 1 = final, 2 = initial, 3 = medial.
        // A right-joining letter has only isolated/final.
        private static readonly Dictionary<char, int> ClassOf = new Dictionary<char, int>
        {
            ['\u0621'] = NonJoining,   // ء
            ['\u0622'] = RightJoining, // آ
            ['\u0623'] = RightJoining, // أ
            ['\u0624'] = RightJoining, // ؤ
            ['\u0625'] = RightJoining, // إ
            ['\u0626'] = DualJoining,  // ئ
            ['\u0627'] = RightJoining, // ا
            ['\u0628'] = DualJoining,  // ب
            ['\u0629'] = RightJoining, // ة
            ['\u062A'] = DualJoining,  // ت
            ['\u062B'] = DualJoining,  // ث
            ['\u062C'] = DualJoining,  // ج
            ['\u062D'] = DualJoining,  // ح
            ['\u062E'] = DualJoining,  // خ
            ['\u062F'] = RightJoining, // د
            ['\u0630'] = RightJoining, // ذ
            ['\u0631'] = RightJoining, // ر
            ['\u0632'] = RightJoining, // ز
            ['\u0633'] = DualJoining,  // س
            ['\u0634'] = DualJoining,  // ش
            ['\u0635'] = DualJoining,  // ص
            ['\u0636'] = DualJoining,  // ض
            ['\u0637'] = DualJoining,  // ط
            ['\u0638'] = DualJoining,  // ظ
            ['\u0639'] = DualJoining,  // ع
            ['\u063A'] = DualJoining,  // غ
            ['\u0641'] = DualJoining,  // ف
            ['\u0642'] = DualJoining,  // ق
            ['\u0643'] = DualJoining,  // ك
            ['\u0644'] = DualJoining,  // ل
            ['\u0645'] = DualJoining,  // م
            ['\u0646'] = DualJoining,  // ن
            ['\u0647'] = DualJoining,  // ه
            ['\u0648'] = RightJoining, // و
            ['\u0649'] = DualJoining,  // ى
            ['\u064A'] = DualJoining,  // ي
            ['\u067E'] = DualJoining,  // پ
            ['\u0686'] = DualJoining,  // چ
            ['\u0698'] = RightJoining, // ژ
            ['\u06A9'] = DualJoining,  // ک
            ['\u06AF'] = DualJoining,  // گ
            ['\u06CC'] = DualJoining,  // ی
            ['\u06C0'] = RightJoining, // ۂ (Heh with Yeh Above)
        };

        private static readonly Dictionary<char, char[]> Forms = new Dictionary<char, char[]>
        {
            ['\u0621'] = new[] { '\uFE80', '\uFE80', '\uFE80', '\uFE80' },
            ['\u0622'] = new[] { '\uFE81', '\uFE82', '\uFE81', '\uFE82' },
            ['\u0623'] = new[] { '\uFE83', '\uFE84', '\uFE83', '\uFE84' },
            ['\u0624'] = new[] { '\uFE85', '\uFE86', '\uFE85', '\uFE86' },
            ['\u0625'] = new[] { '\uFE87', '\uFE88', '\uFE87', '\uFE88' },
            ['\u0626'] = new[] { '\uFE89', '\uFE8A', '\uFE8B', '\uFE8C' },
            ['\u0627'] = new[] { '\uFE8D', '\uFE8E', '\uFE8D', '\uFE8E' },
            ['\u0628'] = new[] { '\uFE8F', '\uFE90', '\uFE91', '\uFE92' },
            ['\u0629'] = new[] { '\uFE93', '\uFE94', '\uFE93', '\uFE94' },
            ['\u062A'] = new[] { '\uFE95', '\uFE96', '\uFE97', '\uFE98' },
            ['\u062B'] = new[] { '\uFE99', '\uFE9A', '\uFE9B', '\uFE9C' },
            ['\u062C'] = new[] { '\uFE9D', '\uFE9E', '\uFE9F', '\uFEA0' },
            ['\u062D'] = new[] { '\uFEA1', '\uFEA2', '\uFEA3', '\uFEA4' },
            ['\u062E'] = new[] { '\uFEA5', '\uFEA6', '\uFEA7', '\uFEA8' },
            ['\u062F'] = new[] { '\uFEA9', '\uFEAA', '\uFEA9', '\uFEAA' },
            ['\u0630'] = new[] { '\uFEAB', '\uFEAC', '\uFEAB', '\uFEAC' },
            ['\u0631'] = new[] { '\uFEAD', '\uFEAE', '\uFEAD', '\uFEAE' },
            ['\u0632'] = new[] { '\uFEAF', '\uFEB0', '\uFEAF', '\uFEB0' },
            ['\u0633'] = new[] { '\uFEB1', '\uFEB2', '\uFEB3', '\uFEB4' },
            ['\u0634'] = new[] { '\uFEB5', '\uFEB6', '\uFEB7', '\uFEB8' },
            ['\u0635'] = new[] { '\uFEB9', '\uFEBA', '\uFEBB', '\uFEBC' },
            ['\u0636'] = new[] { '\uFEBD', '\uFEBE', '\uFEBF', '\uFEC0' },
            ['\u0637'] = new[] { '\uFEC1', '\uFEC2', '\uFEC3', '\uFEC4' },
            ['\u0638'] = new[] { '\uFEC5', '\uFEC6', '\uFEC7', '\uFEC8' },
            ['\u0639'] = new[] { '\uFEC9', '\uFECA', '\uFECB', '\uFECC' },
            ['\u063A'] = new[] { '\uFECD', '\uFECE', '\uFECF', '\uFED0' },
            ['\u0641'] = new[] { '\uFED1', '\uFED2', '\uFED3', '\uFED4' },
            ['\u0642'] = new[] { '\uFED5', '\uFED6', '\uFED7', '\uFED8' },
            ['\u0643'] = new[] { '\uFED9', '\uFEDA', '\uFEDB', '\uFEDC' },
            ['\u0644'] = new[] { '\uFEDD', '\uFEDE', '\uFEDF', '\uFEE0' },
            ['\u0645'] = new[] { '\uFEE1', '\uFEE2', '\uFEE3', '\uFEE4' },
            ['\u0646'] = new[] { '\uFEE5', '\uFEE6', '\uFEE7', '\uFEE8' },
            ['\u0647'] = new[] { '\uFEE9', '\uFEEA', '\uFEEB', '\uFEEC' },
            ['\u0648'] = new[] { '\uFEED', '\uFEEE', '\uFEED', '\uFEEE' },
            ['\u0649'] = new[] { '\uFEEF', '\uFEF0', '\uFEEF', '\uFEF0' },
            ['\u064A'] = new[] { '\uFEF1', '\uFEF2', '\uFEF3', '\uFEF4' },
            ['\u067E'] = new[] { '\uFB56', '\uFB57', '\uFB58', '\uFB59' },
            ['\u0686'] = new[] { '\uFB7A', '\uFB7B', '\uFB7C', '\uFB7D' },
            ['\u0698'] = new[] { '\uFB8A', '\uFB8B', '\uFB8A', '\uFB8B' },
            ['\u06A9'] = new[] { '\uFB8E', '\uFB8F', '\uFB90', '\uFB91' },
            ['\u06AF'] = new[] { '\uFB92', '\uFB93', '\uFB94', '\uFB95' },
            ['\u06CC'] = new[] { '\uFBFC', '\uFBFD', '\uFBFE', '\uFBFF' },
            ['\u06C0'] = new[] { '\u06C0', '\uFBA5', '\u06C0', '\uFBA5' },
        };

        // lam + alef family ligatures: isolated and final.
        private static readonly Dictionary<char, char[]> LamAlef = new Dictionary<char, char[]>
        {
            ['\u0622'] = new[] { '\uFEF5', '\uFEF6' },
            ['\u0623'] = new[] { '\uFEF7', '\uFEF8' },
            ['\u0625'] = new[] { '\uFEF9', '\uFEFA' },
            ['\u0627'] = new[] { '\uFEFB', '\uFEFC' },
        };

        private static readonly Dictionary<char, char> Mirrors = new Dictionary<char, char>
        {
            ['('] = ')', [')'] = '(',
            ['['] = ']', [']'] = '[',
            ['{'] = '}', ['}'] = '{',
            ['<'] = '>', ['>'] = '<',
        };

        internal static bool IsPresentationForm(char c) => c >= '\uFB50' && c <= '\uFEFF';

        internal static bool ContainsPersian(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
            {
                if (c >= '\u0600' && c <= '\u06FF') return true;
                if (IsPresentationForm(c)) return true;
            }
            return false;
        }

        private static bool IsTransparentForJoining(char c)
        {
            if (c == Tatweel) return true;
            if (c >= '\u064B' && c <= '\u065F') return true; // harakat
            if (c >= '\u0670' && c <= '\u0670') return true; // superscript alef
            if (c >= '\u06D6' && c <= '\u06ED') return true; // Quranic marks
            return false;
        }

        private static int ClassOfChar(char c)
        {
            if (IsTransparentForJoining(c)) return Transparent;
            return ClassOf.TryGetValue(c, out int k) ? k : NonJoining;
        }

        // ---- stage 1: shaping ------------------------------------------------

        /// <summary>Map letters to their contextual presentation forms (logical order kept).</summary>
        internal static string Shape(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var chars = input.ToCharArray();
            var sb = new StringBuilder(chars.Length + 8);
            var classes = new int[chars.Length];
            for (int i = 0; i < chars.Length; i++) classes[i] = ClassOfChar(chars[i]);

            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                int cls = classes[i];

                if (cls == Transparent)
                {
                    sb.Append(c);
                    continue;
                }

                if (cls == NonJoining || !Forms.TryGetValue(c, out char[] forms))
                {
                    sb.Append(c);
                    continue;
                }

                int prev = PrevSignificant(classes, i);
                int next = NextSignificant(classes, i);

                bool prevJoins = prev >= 0 && classes[prev] == DualJoining;
                bool nextJoins = next >= 0 && (classes[next] == DualJoining || classes[next] == RightJoining);

                // lam + alef becomes one ligature glyph
                if (c == '\u0644' && next >= 0 && LamAlef.TryGetValue(chars[next], out char[] lig))
                {
                    sb.Append(prevJoins ? lig[1] : lig[0]);
                    i = next; // consume the alef
                    continue;
                }

                // heh + hamza above (هٔ) becomes precomposed ۂ (\u06C0 / \uFBA5)
                if (c == '\u0647' && i + 1 < chars.Length && chars[i + 1] == '\u0654')
                {
                    sb.Append(prevJoins ? '\uFBA5' : '\u06C0');
                    i++; // consume the hamza
                    continue;
                }

                int form;
                if (cls == DualJoining)
                    form = prevJoins ? (nextJoins ? 3 : 1) : (nextJoins ? 2 : 0);
                else
                    form = prevJoins ? 1 : 0;

                sb.Append(forms[form]);
            }

            return sb.ToString();
        }

        private static int PrevSignificant(int[] classes, int index)
        {
            for (int i = index - 1; i >= 0; i--)
                if (classes[i] != Transparent) return i;
            return -1;
        }

        private static int NextSignificant(int[] classes, int index)
        {
            for (int i = index + 1; i < classes.Length; i++)
                if (classes[i] != Transparent) return i;
            return -1;
        }

        // ---- stage 2: bidi reordering ---------------------------------------

        private enum Dir { Rtl, Ltr }

        private static bool IsLtrChar(char c)
        {
            if (c >= 'A' && c <= 'Z') return true;
            if (c >= 'a' && c <= 'z') return true;
            if (c >= '0' && c <= '9') return true;
            if (c >= '\u06F0' && c <= '\u06F9') return true; // Persian digits
            if (c >= '\u0660' && c <= '\u0669') return true; // Arabic-Indic digits
            // Symbols that belong with a Latin/numeric run rather than floating as neutrals.
            if (c == '\u2122' || c == '\u00A9' || c == '\u00AE') return true; // ™ © ®
            if (c == '\u00B0' || c == '%') return true;                       // ° %
            return false;
        }

        /// <summary>Marks that must stay attached to the character they modify.</summary>
        private static bool IsCombiningMark(char c)
        {
            if (c >= '\u0300' && c <= '\u036F') return true; // general diacritics
            if (c >= '\u0610' && c <= '\u061A') return true;
            if (c >= '\u064B' && c <= '\u065F') return true; // harakat, hamza above/below
            if (c == '\u0670') return true;
            if (c >= '\u06D6' && c <= '\u06ED') return true;
            return false;
        }

        private static bool IsNeutral(char c)
        {
            if (IsLtrChar(c)) return false;
            if (c >= '\u0600' && c <= '\u06FF') return false;
            if (IsPresentationForm(c)) return false;
            return true;
        }

        /// <summary>
        /// Reorder shaped text into visual order for a left-to-right renderer.
        /// Latin/digit runs keep their internal order; neutral characters between two
        /// Latin characters join that Latin run; paired punctuation is mirrored.
        /// </summary>
        internal static string Reorder(string shaped)
        {
            if (string.IsNullOrEmpty(shaped)) return shaped;

            var runs = new List<KeyValuePair<Dir, string>>();
            var current = new StringBuilder();
            Dir currentDir = Dir.Rtl;
            bool started = false;

            for (int i = 0; i < shaped.Length; i++)
            {
                char c = shaped[i];
                Dir d;
                if (IsLtrChar(c)) d = Dir.Ltr;
                else if (IsNeutral(c))
                {
                    int n = i + 1;
                    while (n < shaped.Length && IsNeutral(shaped[n])) n++;
                    bool ltrNeighbour = n < shaped.Length && IsLtrChar(shaped[n]);
                    d = ltrNeighbour && started && currentDir == Dir.Ltr ? Dir.Ltr : Dir.Rtl;
                }
                else d = Dir.Rtl;

                if (!started || d != currentDir)
                {
                    if (started) runs.Add(new KeyValuePair<Dir, string>(currentDir, current.ToString()));
                    current.Clear();
                    currentDir = d;
                    started = true;
                }
                current.Append(c);
            }
            if (started) runs.Add(new KeyValuePair<Dir, string>(currentDir, current.ToString()));

            var sb = new StringBuilder(shaped.Length);
            for (int r = runs.Count - 1; r >= 0; r--)
            {
                string text = runs[r].Value;
                if (runs[r].Key == Dir.Ltr)
                {
                    sb.Append(text);
                }
                else
                {
                    // Reverse grapheme clusters, not raw code units: a combining mark such as the
                    // hamza in "هٔ" must stay after its base character or it detaches and the
                    // vowel sign renders on the wrong glyph.
                    var clusters = new List<string>();
                    for (int i = 0; i < text.Length;)
                    {
                        int j = i + 1;
                        while (j < text.Length && IsCombiningMark(text[j])) j++;
                        clusters.Add(text.Substring(i, j - i));
                        i = j;
                    }

                    for (int k = clusters.Count - 1; k >= 0; k--)
                    {
                        string cluster = clusters[k];
                        char baseChar = cluster[0];
                        if (baseChar == Zwnj)
                        {
                            // Join control has already done its job during shaping; rendering it
                            // would risk an empty glyph box.
                            continue;
                        }
                        for (int t = 1; t < cluster.Length; t++) sb.Append(cluster[t]);
                        sb.Append(Mirrors.TryGetValue(baseChar, out char mirror) ? mirror : baseChar);
                    }
                }
            }
            return sb.ToString();
        }

        // ---- tag protection --------------------------------------------------

        private const char SentinelBase = '\uE000';

        private static readonly System.Text.RegularExpressions.Regex StructuralDelimiterRegex =
            new System.Text.RegularExpressions.Regex(
                @"(?i)(<br\s*/?>|<page[^>]*>|<hpage[^>]*>|\r?\n)",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// Shape and reorder a raw localised string for TMP.
        /// Rich-text tags and {n} placeholders survive untouched.
        /// If maxLineLength > 0, long lines are wrapped at word boundaries with &lt;br&gt;
        /// so that lines wrap top-to-bottom and never flip upside down in TextMeshPro.
        /// Delimiters like &lt;br&gt;, &lt;page&gt;, &lt;hpage&gt; are never reversed across.
        /// </summary>
        internal static string ShapeAndReorder(string source, int maxLineLength = 0)
        {
            if (string.IsNullOrEmpty(source)) return source;
            if (HasPresentationForms(source)) return source;
            if (!ContainsPersian(source)) return source;

            string[] tokens = StructuralDelimiterRegex.Split(source);
            if (tokens.Length <= 1 && (maxLineLength <= 0 || source.Length <= maxLineLength))
            {
                return ShapeAndReorderSingleLine(source);
            }

            var sb = new StringBuilder(source.Length + 64);
            foreach (string token in tokens)
            {
                if (string.IsNullOrEmpty(token)) continue;

                if (StructuralDelimiterRegex.IsMatch(token))
                {
                    sb.Append(token);
                }
                else
                {
                    string trimmed = token.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    if (maxLineLength > 0 && GetVisibleLength(trimmed) > maxLineLength)
                    {
                        var wrappedLines = WrapParagraph(trimmed, maxLineLength);
                        for (int i = 0; i < wrappedLines.Count; i++)
                        {
                            if (i > 0) sb.Append("<br>");
                            sb.Append(ShapeAndReorderSingleLine(wrappedLines[i]));
                        }
                    }
                    else
                    {
                        sb.Append(ShapeAndReorderSingleLine(trimmed));
                    }
                }
            }

            return sb.ToString();
        }

        private static string ShapeAndReorderSingleLine(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            if (HasPresentationForms(source)) return source;
            if (!ContainsPersian(source)) return source;

            var originals = new List<string>();
            string protectedText = Protect(source, originals);

            string result = Reorder(Shape(protectedText));

            if (originals.Count == 0) return result;

            var sb = new StringBuilder(result.Length + 64);
            foreach (char c in result)
            {
                int idx = c - SentinelBase;
                if (idx >= 0 && idx < originals.Count) sb.Append(originals[idx]);
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static List<string> WrapParagraph(string text, int maxLen)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text) || GetVisibleLength(text) <= maxLen)
            {
                lines.Add(text);
                return lines;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var current = new StringBuilder();
            int currentLen = 0;

            foreach (string word in words)
            {
                int wordLen = GetVisibleLength(word);
                if (current.Length > 0 && currentLen + 1 + wordLen > maxLen)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                    current.Append(word);
                    currentLen = wordLen;
                }
                else
                {
                    if (current.Length > 0)
                    {
                        current.Append(' ');
                        currentLen += 1;
                    }
                    current.Append(word);
                    currentLen += wordLen;
                }
            }

            if (current.Length > 0)
            {
                lines.Add(current.ToString());
            }

            return lines;
        }

        private static int GetVisibleLength(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int len = 0;
            bool inTag = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<') inTag = true;
                else if (c == '>') inTag = false;
                else if (!inTag && c != Zwnj && !IsCombiningMark(c)) len++;
            }
            return len;
        }

        internal static bool HasPresentationForms(string s)
        {
            foreach (char c in s)
                if (IsPresentationForm(c)) return true;
            return false;
        }

        /// <summary>Replace &lt;tag&gt; and {n} sequences with single private-use sentinels.</summary>
        private static string Protect(string source, List<string> originals)
        {
            var sb = new StringBuilder(source.Length);
            int i = 0;
            while (i < source.Length)
            {
                char c = source[i];
                if (c == '<')
                {
                    int close = source.IndexOf('>', i + 1);
                    if (close > i)
                    {
                        sb.Append(Sentinel(originals, source.Substring(i, close - i + 1)));
                        i = close + 1;
                        continue;
                    }
                }
                else if (c == '{')
                {
                    int close = source.IndexOf('}', i + 1);
                    if (close > i && close - i <= 4)
                    {
                        bool digits = true;
                        for (int k = i + 1; k < close; k++)
                            if (source[k] < '0' || source[k] > '9') { digits = false; break; }
                        if (digits)
                        {
                            sb.Append(Sentinel(originals, source.Substring(i, close - i + 1)));
                            i = close + 1;
                            continue;
                        }
                    }
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        private static char Sentinel(List<string> originals, string tag)
        {
            int index = originals.Count;
            originals.Add(tag);
            if (index >= 512) return ' '; // bounded: never overflow the private-use window
            return (char)(SentinelBase + index);
        }
    }
}
