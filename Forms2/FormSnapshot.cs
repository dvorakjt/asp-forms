using System;
using System.Collections.Generic;

namespace FormsSandbox.Forms2
{
    // A snapshot tracks reads. Conditions may inspect invalid/excluded states;
    // validators use RequireValid to skip when a prerequisite is unavailable.
    public sealed class FormSnapshot
    {
        private readonly IDictionary<string, IFieldState> fields;
        private readonly Func<string, bool> isAwaitingAsync;
        private readonly Func<string, bool> isAwaitingLocal;
        private readonly Action<string> resolveInclusion;
        private readonly Stack<HashSet<string>> readScopes = new Stack<HashSet<string>>();

        internal FormSnapshot(IDictionary<string, IFieldState> fields,
            Action<string> resolveInclusion, Func<string, bool> isAwaitingAsync,
            Func<string, bool> isAwaitingLocal)
        {
            this.fields = fields;
            this.resolveInclusion = resolveInclusion;
            this.isAwaitingAsync = isAwaitingAsync;
            this.isAwaitingLocal = isAwaitingLocal;
        }

        public FieldState<T> Inspect<T>(string name)
        {
            if (!fields.TryGetValue(name, out var field))
                throw new InvalidOperationException("Unknown field: " + name);
            foreach (var scope in readScopes) scope.Add(name);
            resolveInclusion?.Invoke(name);
            if (!(field is FieldState<T> typed))
                throw new InvalidOperationException("Wrong field type: " + name);
            return typed;
        }

        public FieldState<T> RequireValid<T>(string name)
        {
            var field = Inspect<T>(name);
            if (isAwaitingAsync != null && isAwaitingAsync(name) ||
                isAwaitingLocal != null && isAwaitingLocal(name))
                throw new DeferredFieldException(name);
            if (!field.IsValid) throw new UnavailableFieldException(name);
            return field;
        }

        internal void BeginReads() { readScopes.Push(new HashSet<string>(StringComparer.Ordinal)); }
        internal string[] EndReads()
        {
            var scope = readScopes.Pop();
            var result = new string[scope.Count];
            scope.CopyTo(result);
            return result;
        }
    }

    internal sealed class UnavailableFieldException : Exception
    {
        public UnavailableFieldException(string name) : base(name) { }
    }

    internal sealed class DeferredFieldException : Exception
    {
        public DeferredFieldException(string name) : base(name) { }
    }
}