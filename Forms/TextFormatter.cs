using System;

namespace FormsSandbox.Forms
{
    // A formatter changes displayed text only. Browser event handling is not
    // wired up yet; its implementation and identifier are defined together.
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