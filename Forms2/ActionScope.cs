using System;
using System.Linq;

namespace FormsSandbox.Forms2
{
    public sealed class ActionScope
    {
        public string[] Include { get; set; }
        public string[] Exclude { get; set; }
        public Func<FormSnapshot, bool> Condition { get; set; }

        public bool Applies(string action, FormSnapshot snapshot)
        {
            if (Include != null && Exclude != null)
                throw new InvalidOperationException("Include and Exclude are mutually exclusive.");
            if (Include != null && !Include.Contains(action, StringComparer.Ordinal)) return false;
            if (Exclude != null && Exclude.Contains(action, StringComparer.Ordinal)) return false;
            return Condition == null || Condition(snapshot);
        }
    }
}