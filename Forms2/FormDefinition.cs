using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace FormsSandbox.Forms2
{
    public sealed class FormDefinition<TModel> where TModel : new()
    {
        private readonly IList<IFieldDefinition> fields = new List<IFieldDefinition>();
        public IList<FormAction> Actions { get; } = new List<FormAction>();
        public string DefaultAction { get; set; }

        public FieldDefinition<T> Field<T>(Expression<Func<TModel, T>> selector)
        {
            if (!(selector?.Body is MemberExpression member) ||
                !(member.Expression is ParameterExpression) || !(member.Member is PropertyInfo property) ||
                !property.CanWrite || !property.CanRead || property.PropertyType != typeof(T))
                throw new ArgumentException("Select a writable direct model property.", nameof(selector));
            var definition = new FieldDefinition<T>(property);
            fields.Add(definition);
            return definition;
        }

        public void CheckConfiguration()
        {
            if (Actions.Count == 0 || !Actions.Any(a => a.Name == DefaultAction) ||
                Actions.Any(a => string.IsNullOrEmpty(a.Name) || string.IsNullOrEmpty(a.Endpoint) ||
                    string.IsNullOrEmpty(a.Controller)) ||
                Actions.Select(a => a.Name).Distinct(StringComparer.Ordinal).Count() != Actions.Count)
                throw new InvalidOperationException("Define actions and an explicit default action.");
            if (fields.Select(f => f.Name).Distinct(StringComparer.Ordinal).Count() != fields.Count)
                throw new InvalidOperationException("Field names must be distinct.");
            foreach (var field in fields)
            {
                CheckScope(field.Scope);
                field.CheckConfiguration();
                    foreach (var validatorScope in field.ValidatorScopes) CheckScope(validatorScope);
                if (field.Orders.Distinct().Count() != field.Orders.Count())
                    throw new InvalidOperationException("Validator orders must be unique for " + field.Name);
            }
        }

        private void CheckScope(ActionScope scope)
        {
            if (scope == null || scope.Include != null && scope.Exclude != null)
                throw new InvalidOperationException("Invalid action scope.");
            foreach (var name in (scope.Include ?? new string[0]).Concat(scope.Exclude ?? new string[0]))
                if (!Actions.Any(a => a.Name == name))
                    throw new InvalidOperationException("Unknown action " + name);
            if (scope.Condition != null && (!scope.Condition.Method.IsStatic ||
                !scope.Condition.Method.IsPublic || scope.Condition.Method.DeclaringType == null ||
                !scope.Condition.Method.DeclaringType.IsPublic))
                throw new InvalidOperationException("Client conditions must be named public static methods.");
        }

        public async Task<FormResult<TModel>> ValidateAsync(string action,
            Func<string, string[]> read, ModelStateDictionary modelState = null)
        {
            if (read == null) throw new ArgumentNullException(nameof(read));
            CheckConfiguration();
            if (!Actions.Any(a => a.Name == action))
                throw new InvalidOperationException("Unknown action: " + action);

            var states = fields.ToDictionary(f => f.Name, f => f.Convert(read(f.Name)),
                StringComparer.Ordinal);
            foreach (var field in fields) field.Normalize(states[field.Name]);

            var resolved = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var pendingAsync = new HashSet<string>(StringComparer.Ordinal);
            var pendingLocal = new HashSet<string>(StringComparer.Ordinal);
            var reads = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            FormSnapshot snapshot = null;
            Action<string> resolve = null;
            resolve = name =>
            {
                if (resolved.Contains(name)) return;
                if (!visiting.Add(name))
                    throw new InvalidOperationException("Participation cycle involving " + name);
                var field = fields.Single(f => f.Name == name);
                snapshot.BeginReads();
                try { states[name].SetIncluded(field.Scope.Applies(action, snapshot)); }
                catch (UnavailableFieldException) { states[name].SetIncluded(false); }
                finally
                {
                    AddReads("include:" + name, snapshot.EndReads());
                    visiting.Remove(name);
                }
                resolved.Add(name);
                if (states[name].IsValid)
                {
                    if (field.HasLocalValidator) pendingLocal.Add(name);
                    try
                    {
                        field.ValidateAsync(states[name], snapshot, action, false, false, AddReads)
                            .GetAwaiter().GetResult();
                        pendingLocal.Remove(name);
                    }
                    catch (DeferredFieldException) { /* Validate once the prerequisite is ready. */ }
                }
            };
            snapshot = new FormSnapshot(states, resolve, name => pendingAsync.Contains(name),
                name => pendingLocal.Contains(name));
            foreach (var field in fields) resolve(field.Name);

            while (pendingLocal.Count != 0)
            {
                var progressed = false;
                foreach (var field in fields)
                {
                    if (!pendingLocal.Contains(field.Name)) continue;
                    try
                    {
                        await field.ValidateAsync(states[field.Name], snapshot, action,
                            false, false, AddReads).ConfigureAwait(false);
                    }
                    catch (DeferredFieldException) { continue; }
                    pendingLocal.Remove(field.Name);
                    progressed = true;
                }
                if (!progressed)
                    throw new InvalidOperationException("Circular local validation prerequisites.");
            }

            foreach (var field in fields)
                if (states[field.Name].IsValid)
                    await field.ValidateAsync(states[field.Name], snapshot, action,
                        true, false, AddReads).ConfigureAwait(false);

            foreach (var field in fields)
                if (states[field.Name].IsValid && field.HasCrossFieldAsyncValidator &&
                    field.IsApplicableAsync(action, snapshot))
                    pendingAsync.Add(field.Name);
            while (pendingAsync.Count != 0)
            {
                var progressed = false;
                foreach (var field in fields)
                {
                    if (!pendingAsync.Contains(field.Name)) continue;
                    try
                    {
                        await field.ValidateAsync(states[field.Name], snapshot, action,
                            true, true, AddReads).ConfigureAwait(false);
                    }
                    catch (DeferredFieldException) { continue; }
                    pendingAsync.Remove(field.Name);
                    progressed = true;
                }
                if (!progressed)
                    throw new InvalidOperationException("Circular server-only async validator dependencies.");
            }

            var model = new TModel();
            foreach (var field in fields)
            {
                var state = states[field.Name];
                if (state.IsValid) field.Assign(model, state);
                else if (state.IsIncluded && state.Error != null)
                    modelState?.AddModelError(field.Name, state.Error);
            }
            return new FormResult<TModel> { Model = model, Fields = states,
                IsValid = states.Values.All(s => !s.IsIncluded || s.IsValid),
                Dependencies = reads.ToDictionary(p => p.Key, p => p.Value.ToArray()) };

            void AddReads(string key, string[] names)
            {
                if (!reads.TryGetValue(key, out var set))
                    reads[key] = set = new HashSet<string>(StringComparer.Ordinal);
                foreach (var name in names) set.Add(name);
            }
        }

        public string ClientConfiguration()
        {
            CheckConfiguration();
            return new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new
            {
                defaultAction = DefaultAction,
                actions = Actions.Select(a => new { name = a.Name, controller = a.Controller,
                    endpoint = a.Endpoint }).ToArray(),
                fields = fields.Select(f => new {
                    name = f.Name, type = f.ValueType.Name,
                    include = f.Scope.Include, exclude = f.Scope.Exclude,
                    condition = f.Scope.Condition?.Method.Name,
                    input = f.InputSettings,
                    normalize = f.NormalizationMethod,
                    validators = f.ClientValidators
                }).ToArray()
            });
        }
    }
}