using System;

namespace FormsSandbox.Forms
{
    // A formatter changes displayed text only. Named public static methods
    // link by method name; anonymous callbacks need an explicit RuleId.
    public sealed class TextFormatter
    {
        public string RuleId { get; set; }
        public Func<string, string> Format { get; set; }

        public string Preview(string text)
        {
            if (Format == null) throw new InvalidOperationException("Formatter needs a function.");
            return Format(text) ?? throw new InvalidOperationException("A formatter returned null.");
        }
    }
}