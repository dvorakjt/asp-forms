using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FormsSandbox.Forms
{
    public sealed class InputBlocker
    {
        public InputBlockerKind Kind { get; set; }
        public int Order { get; set; }
        public int? MaximumLength { get; set; }
        // The pattern must accept valid intermediate text (e.g. "12345-").
        public string PrefixPattern { get; set; }
        public string RuleId { get; set; }
        public Func<string, IDictionary<string, string>, bool> CustomCheck { get; set; }

        public bool Allows(string candidate, IDictionary<string, string> formValues)
        {
            switch (Kind)
            {
                case InputBlockerKind.MaxLength: return candidate.Length <= MaximumLength.Value;
                case InputBlockerKind.Pattern: return Regex.IsMatch(candidate, PrefixPattern);
                case InputBlockerKind.Custom: return CustomCheck(candidate, formValues);
                default: throw new InvalidOperationException("Unknown input blocker: " + Kind);
            }
        }
    }
}