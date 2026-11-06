using System;

namespace FormsSandbox.Forms
{
    public sealed class TextNormalizer
    {
        public TextNormalizerKind Kind { get; set; }
        public string RuleId { get; set; }
        public Func<string, string> CustomNormalize { get; set; }

        public string Apply(string text)
        {
            switch (Kind)
            {
                case TextNormalizerKind.Trim: return text.Trim();
                case TextNormalizerKind.Custom: return CustomNormalize(text) ??
                    throw new InvalidOperationException("A normalizer returned null.");
                default: throw new InvalidOperationException("Unknown normalizer: " + Kind);
            }
        }
    }
}