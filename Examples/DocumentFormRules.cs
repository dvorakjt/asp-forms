using System.Collections.Generic;

namespace FormsSandbox.Examples
{
    // This application rule is compiled from identical C# by MVC and Transpose.
    public static class DocumentFormRules
    {
        public static bool SummaryDiffersFromTitle(string summary, string title)
        {
            return summary != title;
        }

        public static bool SummaryDiffersFromTitleInForm(
            string summary, IDictionary<string, string> values)
        {
            return SummaryDiffersFromTitle(summary, values["Title"]);
        }

        // A separate browser-only editing rule; server-side validation is authoritative.
        public static bool AmountHasNoEmbeddedSpaces(string proposedText)
        {
            return proposedText.IndexOf(' ') < 0;
        }

        public static bool AmountHasNoEmbeddedSpacesInForm(
            string proposedText, IDictionary<string, string> values)
        {
            return AmountHasNoEmbeddedSpaces(proposedText);
        }

        public static string PhoneDigits(string text)
        {
            var digits = "";
            foreach (var character in text)
                if (character >= '0' && character <= '9') digits += character;
            return digits;
        }

        public static string FormatPhone(string text)
        {
            var digits = PhoneDigits(text);
            if (digits.Length <= 3) return digits;
            if (digits.Length <= 6) return digits.Substring(0, 3) + "-" + digits.Substring(3);
            return digits.Substring(0, 3) + "-" + digits.Substring(3, 3) + "-" + digits.Substring(6);
        }
    }
}