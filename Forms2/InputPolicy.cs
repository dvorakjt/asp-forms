using System;

namespace FormsSandbox.Forms2
{
    // Both functions are optional. Browser editing only; neither replaces
    // authoritative conversion and validation of posted values.
    public sealed class InputPolicy
    {
        public Func<string, bool> Allows { get; set; }
        public Func<string, string> Format { get; set; }
    }
}