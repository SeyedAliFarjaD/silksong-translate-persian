using System;
using System.Text;

namespace SilksongPersian
{
    /// <summary>
    /// Offline checks for the shaping/bidi pipeline, compiled against the same source file the
    /// plugin ships. Verifies joining, ligatures, run ordering and token preservation without
    /// launching the game.
    /// </summary>
    internal static class Program
    {
        private static int _failures;

        private static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;

            Sample("سلام");
            Sample("دستاوردها");
            Sample("شوالیهٔ توخالی: سیلکسونگ");
            Sample("خط اول<br>خط دوم");
            Sample("از {1} پروندهٔ ذخیرهٔ PS4™، {0} مورد درون‌ریزی شد.");
            Sample("حمله با سوزن (سریع)");
            Sample("Load Game");
            Sample("Hollow Knight — شوالیهٔ توخالی");

            Console.WriteLine();
            var br = PersianText.ShapeAndReorder("خط اول<br>خط دوم");
            Assert(br.Contains("<br>"), "rich-text <br> survives");
            Assert(br.StartsWith(PersianText.ShapeAndReorder("خط اول")), "delimiter <br> preserves line 1 before line 2");

            var spacesAroundBr = PersianText.ShapeAndReorder("خط اول <br>  خط دوم");
            Assert(spacesAroundBr == br, "spaces around <br> are trimmed cleanly");

            var wrapped = PersianText.ShapeAndReorder("رشته‌ای چندکاره که طبیعتاً در صدف تولید می‌شود. چون ابریشم کافی ذخیره شود، زخم‌ها شفا می‌یابند و مهارت‌ها اجرا می‌شوند.<br><br>برای بازیابی ابریشم به دشمنان حمله کن.", 28);
            Assert(wrapped.Contains("<br><br>"), "double break preserved");
            var paras = wrapped.Split(new[] { "<br><br>" }, StringSplitOptions.None);
            Assert(paras.Length == 2, "two paragraphs preserved in order");
            Assert(paras[0].Contains("<br>"), "paragraph 1 wrapped into lines");
            Assert(paras[1].Contains("<br>"), "paragraph 2 wrapped into lines");

            var ph = PersianText.ShapeAndReorder("مقدار {0} از {1}");
            Assert(ph.Contains("{0}") && ph.Contains("{1}"), "{n} placeholders survive");

            var once = PersianText.ShapeAndReorder("سلام");
            var twice = PersianText.ShapeAndReorder(once);
            Assert(once == twice, "idempotent: shaped text is not shaped again");

            Assert(HasPresentationForm(once), "letters mapped to presentation forms");
            Assert(!HasPresentationForm("سلام"), "plain input stays plain before shaping");

            var latinOnly = PersianText.ShapeAndReorder("Load Game");
            Assert(latinOnly == "Load Game", "pure Latin text untouched");

            Console.WriteLine(_failures == 0 ? "\nALL CHECKS PASSED" : $"\n{_failures} CHECK(S) FAILED");
            Environment.Exit(_failures == 0 ? 0 : 1);
        }

        private static void Sample(string input)
        {
            string shaped = PersianText.Shape(input);
            string final = PersianText.ShapeAndReorder(input);
            Console.WriteLine($"in     : {input}");
            Console.WriteLine($"shaped : {shaped}");
            Console.WriteLine($"visual : {final}");
            Console.WriteLine($"codes  : {Codes(final)}");
            Console.WriteLine();
        }

        private static string Codes(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) sb.Append($"U+{(int)c:X4} ");
            return sb.ToString().Trim();
        }

        private static bool HasPresentationForm(string s)
        {
            foreach (char c in s)
                if (c >= '\uFB50' && c <= '\uFEFF') return true;
            return false;
        }

        private static void Assert(bool condition, string label)
        {
            Console.WriteLine($"{(condition ? "PASS" : "FAIL")}  {label}");
            if (!condition) _failures++;
        }
    }
}
