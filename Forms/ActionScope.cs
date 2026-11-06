using System;
using System.Linq;

namespace FormsSandbox.Forms
{
    public sealed class ActionScope
    {
        public string[] Include { get; set; }
        public string[] Exclude { get; set; }

        public bool Applies(string action)
        {
            if (Include != null && Exclude != null)
                throw new InvalidOperationException("Include and Exclude cannot both be set.");

            if (Include != null) return Include.Contains(action, StringComparer.Ordinal);
            return Exclude == null || !Exclude.Contains(action, StringComparer.Ordinal);
        }
    }
}