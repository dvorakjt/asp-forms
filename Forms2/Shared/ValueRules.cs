using System;

namespace FormsSandbox.Forms2.Shared
{
    // Deliberately specified semantics, compiled from the same C# for CLR and JS.
    public static class ValueRules
    {
        public static string Trim(string text)
        {
            if (text == null) return "";
            // U+0085 is explicitly included; do not rely on JS \s or CLR Trim defaults.
            return text.Trim(' ', '\t', '\r', '\n', '\u0085', '\u00a0');
        }

        public static bool AsciiDigits(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var character in text)
                if (character < '0' || character > '9') return false;
            return true;
        }

        public static bool DecimalSyntax(string text)
        {
            return NumericSyntax(text, 28);
        }

        public static bool FloatingSyntax(string text)
        {
            return NumericSyntax(text, int.MaxValue);
        }

        private static bool NumericSyntax(string text, int maxFractionalDigits)
        {
            if (string.IsNullOrEmpty(text)) return false;
            var start = text[0] == '-' || text[0] == '+' ? 1 : 0;
            var dot = false;
            var digits = 0;
            var fractionalDigits = 0;
            for (var index = start; index < text.Length; index++)
            {
                var character = text[index];
                if (character == '.' && !dot) { dot = true; continue; }
                if (character < '0' || character > '9') return false;
                digits++;
                if (dot) fractionalDigits++;
            }
            return digits > 0 && fractionalDigits <= maxFractionalDigits;
        }
    }
}