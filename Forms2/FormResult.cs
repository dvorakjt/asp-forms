using System.Collections.Generic;

namespace FormsSandbox.Forms2
{
    public sealed class FormResult<TModel>
    {
        public TModel Model { get; internal set; }
        public IDictionary<string, IFieldState> Fields { get; internal set; }
        public IDictionary<string, string[]> Dependencies { get; internal set; }
        public bool IsValid { get; internal set; }
    }
}