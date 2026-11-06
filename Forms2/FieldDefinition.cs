using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace FormsSandbox.Forms2
{
    internal interface IFieldDefinition
    {
        string Name { get; }
        ActionScope Scope { get; }
        IFieldState Convert(string[] values);
        void Normalize(IFieldState field);
        void Assign(object model, IFieldState field);
        Task ValidateAsync(IFieldState field, FormSnapshot snapshot, string action,
            bool crossField, bool serverOnly, Action<string, string[]> recordReads);
        IEnumerable<int> Orders { get; }
        void CheckConfiguration();
        Type ValueType { get; }
        string NormalizationMethod { get; }
        object InputSettings { get; }
        object[] ClientValidators { get; }
        IEnumerable<ActionScope> ValidatorScopes { get; }
        bool HasLocalValidator { get; }
        bool HasCrossFieldAsyncValidator { get; }
        bool IsApplicableAsync(string action, FormSnapshot snapshot);
    }

    public sealed class FieldDefinition<T> : IFieldDefinition
    {
        private readonly PropertyInfo property;
        internal FieldDefinition(PropertyInfo property) { this.property = property; }

        public string Name => property.Name;
        public ActionScope Scope { get; set; } = new ActionScope();
        public InputPolicy Input { get; set; }
        public Func<T, T> NormalizeValue { get; set; }
        public IList<FieldValidator<T>> Validators { get; } = new List<FieldValidator<T>>();
        Type IFieldDefinition.ValueType => Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        string IFieldDefinition.NormalizationMethod => NormalizeValue?.Method.Name;
        object IFieldDefinition.InputSettings => Input == null ? null : new {
            allow = Input.Allows?.Method.Name, format = Input.Format?.Method.Name
        };
        object[] IFieldDefinition.ClientValidators => Validators.Where(v => v.Check != null)
            .Select(v => (object)new { order = v.Order, message = v.Message,
                crossField = v.CrossField, method = v.Check.Method.Name,
                include = v.Scope.Include, exclude = v.Scope.Exclude,
                condition = v.Scope.Condition?.Method.Name }).ToArray();
       IEnumerable<ActionScope> IFieldDefinition.ValidatorScopes => Validators.Select(v => v.Scope);
    bool IFieldDefinition.HasLocalValidator => Validators.Any(v => !v.CrossField);
        bool IFieldDefinition.HasCrossFieldAsyncValidator => Validators.Any(v =>
            v.CrossField && v.CheckOnServerAsync != null);
        bool IFieldDefinition.IsApplicableAsync(string action, FormSnapshot snapshot)
        {
            foreach (var validator in Validators.Where(v => v.CrossField &&
                v.CheckOnServerAsync != null))
            {
                try { if (validator.Scope.Applies(action, snapshot)) return true; }
                catch (UnavailableFieldException) { }
            }
            return false;
        }
        IEnumerable<int> IFieldDefinition.Orders
        {
            get { foreach (var validator in Validators) yield return validator.Order; }
        }

        void IFieldDefinition.CheckConfiguration()
        {
            if (Input != null)
            {
                foreach (var method in new Delegate[] { Input.Allows, Input.Format })
                    if (method != null && (!method.Method.IsStatic || !method.Method.IsPublic ||
                        method.Method.DeclaringType == null || !method.Method.DeclaringType.IsPublic))
                        throw new InvalidOperationException("Input policy methods must be named public static methods.");
            }
            var actual = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (actual != typeof(string) && actual != typeof(bool) &&
                actual != typeof(int) && actual != typeof(decimal) &&
                actual != typeof(float) && actual != typeof(double))
                throw new InvalidOperationException("Unsupported model property type: " + actual.Name);
            if (NormalizeValue != null && (!NormalizeValue.Method.IsStatic ||
                !NormalizeValue.Method.IsPublic || NormalizeValue.Method.DeclaringType == null ||
                !NormalizeValue.Method.DeclaringType.IsPublic))
                throw new InvalidOperationException("Typed normalizers must be named public static methods.");
            foreach (var validator in Validators)
            {
                if (validator == null || validator.Message == null || validator.Scope == null ||
                    (validator.Check == null) == (validator.CheckOnServerAsync == null))
                    throw new InvalidOperationException("Each validator needs one check and a message.");
                if (validator.CheckOnServerAsync != null && !validator.CrossField)
                    throw new InvalidOperationException("Server-only async validators must be cross-field rules.");
                if (validator.Check != null && (!validator.Check.Method.IsStatic ||
                    !validator.Check.Method.IsPublic || validator.Check.Method.DeclaringType == null ||
                    !validator.Check.Method.DeclaringType.IsPublic))
                    throw new InvalidOperationException("Client validators must be named public static methods.");
            }
        }

        IFieldState IFieldDefinition.Convert(string[] values)
        {
            if (values != null && values.Length > 1)
                return new FieldState<T> { Name = Name, Raw = values[0],
                    IsPresent = true, ConversionSucceeded = false,
                    Error = "Only one value is allowed." };
            var raw = values == null || values.Length == 0 ? "" : values[0] ?? "";
            var result = FieldConversion.Parse<T>(raw);
            return new FieldState<T> { Name = Name, Raw = raw,
                IsPresent = raw.Length != 0, ConversionSucceeded = result.Succeeded,
                Value = result.Value, Error = result.Error };
        }

        void IFieldDefinition.Normalize(IFieldState field)
        {
            var state = (FieldState<T>)field;
            if (state.ConversionSucceeded && NormalizeValue != null)
                state.Value = NormalizeValue(state.Value);
        }

        void IFieldDefinition.Assign(object model, IFieldState field)
        {
            property.SetValue(model, ((FieldState<T>)field).Value);
        }

        async Task IFieldDefinition.ValidateAsync(IFieldState field, FormSnapshot snapshot,
            string action, bool crossField, bool serverOnly, Action<string, string[]> recordReads)
        {
            var state = (FieldState<T>)field;
            if (!state.IsValid) return;
            foreach (var validator in Validators)
            {
                if (validator.CrossField != crossField ||
                    (validator.CheckOnServerAsync != null) != serverOnly) continue;
                snapshot.BeginReads();
                try
                {
                    if (!validator.Scope.Applies(action, snapshot)) continue;
                    var passed = serverOnly
                        ? await validator.CheckOnServerAsync(state, snapshot).ConfigureAwait(false)
                        : validator.Check(state, snapshot);
                    if (!passed) state.Error = validator.Message;
                }
                catch (UnavailableFieldException) { /* A prerequisite is excluded or invalid. */ }
                finally
                {
                    var dependencies = snapshot.EndReads();
                    if (!crossField && dependencies.Length != 0)
                        throw new InvalidOperationException("A field-local validator read another field.");
                    recordReads(Name, dependencies);
                }
                if (state.Error != null) break;
            }
        }
    }
}