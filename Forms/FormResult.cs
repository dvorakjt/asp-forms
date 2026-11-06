using System.Collections.Generic;

namespace FormsSandbox.Forms
{
    public sealed class FormResult
    {
        public IDictionary<string, object> Values { get; } = new Dictionary<string, object>();
        public IDictionary<string, string> Errors { get; } = new Dictionary<string, string>();
        public bool IsValid => Errors.Count == 0;
    }
}