using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Web.Mvc;
using System.Web.Script.Serialization;

namespace FormsSandbox.Forms
{
    public sealed class FormDefinition
    {
        private readonly Type modelType;

        public FormDefinition() { }

        private FormDefinition(Type modelType) { this.modelType = modelType; }

        public static FormDefinition For<TModel>() => new FormDefinition(typeof(TModel));

        public FormField Field<TModel, TValue>(Expression<Func<TModel, TValue>> property)
        {
            if (modelType != typeof(TModel))
                throw new InvalidOperationException("The property must belong to this form's model.");
            if (!(property?.Body is MemberExpression member) ||
                !(member.Expression is ParameterExpression) || !(member.Member is PropertyInfo))
                throw new ArgumentException("Select a direct model property.", nameof(property));

            var field = new FormField { Name = member.Member.Name };
            Fields.Add(field);
            return field;
        }

        public IList<FormAction> Actions { get; } = new List<FormAction>();
        public IList<FormField> Fields { get; } = new List<FormField>();

        public FormAction Action(string name)
        {
            var action = Actions.SingleOrDefault(a => a.Name == name);
            if (action == null) throw new InvalidOperationException("Unknown form action: " + name);
            return action;
        }

        public void CheckConfiguration()
        {
            if (Actions.Count == 0 || Actions.Any(a => string.IsNullOrEmpty(a.Name) ||
                string.IsNullOrEmpty(a.Controller) || string.IsNullOrEmpty(a.Endpoint)) ||
                Actions.Select(a => a.Name).Distinct(StringComparer.Ordinal).Count() != Actions.Count)
                throw new InvalidOperationException("Define distinct named actions and endpoints.");
            if (Fields.Select(f => f.Name).Distinct(StringComparer.Ordinal).Count() != Fields.Count)
                throw new InvalidOperationException("Field names must be distinct.");
            foreach (var field in Fields)
            {
                if (field == null) throw new InvalidOperationException("A field cannot be null.");
                if (string.IsNullOrEmpty(field.Name) || !Enum.IsDefined(typeof(FieldElement), field.Element))
                    throw new InvalidOperationException("Unsupported field element.");
                if (modelType != null)
                {
                    var property = modelType.GetProperty(field.Name, BindingFlags.Instance | BindingFlags.Public);
                    if (property == null || !property.CanRead || !property.CanWrite ||
                        (property.PropertyType != typeof(string) && property.PropertyType != typeof(string[])))
                        throw new InvalidOperationException("Field " + field.Name +
                            " must refer to a public writable string or string[] model property.");
                    if ((field.Element == FieldElement.CheckBoxGroup) !=
                        (property.PropertyType == typeof(string[])))
                        throw new InvalidOperationException("Field " + field.Name +
                            " must match the model property collection shape.");
                }
                if (field.Converter == null || field.Validators == null ||
                    field.Blockers == null || field.Options == null ||
                    field.Validators.Any(v => v == null) ||
                    field.Blockers.Any(b => b == null) || field.Options.Any(o => o == null))
                    throw new InvalidOperationException("Field definitions cannot be null.");
                CheckScope(field.Scope);
                if (field.Normalizer != null &&
                    (!Enum.IsDefined(typeof(TextNormalizerKind), field.Normalizer.Kind) ||
                     field.Normalizer.Kind == TextNormalizerKind.Custom &&
                     (string.IsNullOrEmpty(field.Normalizer.RuleId) ||
                      field.Normalizer.CustomNormalize == null)))
                    throw new InvalidOperationException("Unsupported or incomplete normalizer.");
                if (field.Formatter != null &&
                    (string.IsNullOrEmpty(field.Formatter.RuleId) || field.Formatter.Format == null))
                    throw new InvalidOperationException("A formatter needs a rule ID and function.");
                CheckOrders(field.Validators.Select(v => v.Order));
                CheckOrders(field.Blockers.Select(b => b.Order));
                foreach (var validator in field.Validators)
                {
                    CheckScope(validator.Scope);
                    if (validator.OtherField != null && !Fields.Any(f => f.Name == validator.OtherField))
                        throw new InvalidOperationException("Unknown dependent field: " + validator.OtherField);
                    if (!Enum.IsDefined(typeof(ValidatorKind), validator.Kind))
                        throw new InvalidOperationException("Unsupported validator kind.");
                    if (validator.Kind == ValidatorKind.Custom &&
                        (string.IsNullOrEmpty(validator.RuleId) || validator.CustomCheck == null))
                        throw new InvalidOperationException("Custom validators need a rule ID and server check.");
                    if (validator.Kind == ValidatorKind.MinValue && !validator.Minimum.HasValue ||
                        validator.Kind == ValidatorKind.MaxValue && !validator.Maximum.HasValue)
                        throw new InvalidOperationException("Minimum and maximum validators need a bound.");
                    if (validator.Kind == ValidatorKind.Step &&
                        (!validator.StepSize.HasValue || validator.StepSize.Value <= 0m))
                        throw new InvalidOperationException("Step validators need a positive step size.");
                    if ((validator.Kind == ValidatorKind.MinValue ||
                         validator.Kind == ValidatorKind.MaxValue ||
                         validator.Kind == ValidatorKind.Step ||
                         validator.Kind == ValidatorKind.IsNumber) &&
                        (field.Converter.Type == FieldValueType.String ||
                         field.Converter.Type == FieldValueType.Boolean))
                        throw new InvalidOperationException("Numeric validators need a numeric field converter.");
                }
                foreach (var action in Actions)
                {
                    if (!field.Scope.Applies(action.Name) ||
                        !field.Validators.Any(v => v.Kind == ValidatorKind.Step && v.Scope.Applies(action.Name)))
                        continue;

                    if (field.Validators.Count(v => v.Kind == ValidatorKind.MinValue &&
                        v.Scope.Applies(action.Name)) > 1)
                        throw new InvalidOperationException(
                            "A step validator needs at most one active minimum per action.");
                }
                foreach (var blocker in field.Blockers)
                {
                    if (!Enum.IsDefined(typeof(InputBlockerKind), blocker.Kind))
                        throw new InvalidOperationException("Unsupported input blocker kind.");
                    switch (blocker.Kind)
                    {
                        case InputBlockerKind.MaxLength:
                            if (!blocker.MaximumLength.HasValue || blocker.MaximumLength < 0)
                                throw new InvalidOperationException("MaxLength blocker needs a nonnegative limit.");
                            break;
                        case InputBlockerKind.Pattern:
                            if (string.IsNullOrEmpty(blocker.PrefixPattern))
                                throw new InvalidOperationException("Pattern blocker needs a prefix pattern.");
                            break;
                        case InputBlockerKind.Custom:
                            if (string.IsNullOrEmpty(blocker.RuleId) || blocker.CustomCheck == null)
                                throw new InvalidOperationException("Custom blocker needs a rule ID and check.");
                            break;
                    }
                }
                if (!Enum.IsDefined(typeof(FieldValueType), field.Converter.Type))
                    throw new InvalidOperationException("Unsupported converter: " + field.Converter.Type);
                if (field.Element == FieldElement.CheckBox && field.Converter.Type != FieldValueType.Boolean)
                    throw new InvalidOperationException("A checkbox requires Boolean conversion.");
                if (field.Element == FieldElement.CheckBoxGroup && field.Converter.Type != FieldValueType.String)
                    throw new InvalidOperationException("A checkbox group requires String option values.");
                if (field.Converter.Type == FieldValueType.Boolean && field.Element != FieldElement.CheckBox)
                    throw new InvalidOperationException("Boolean conversion requires a checkbox.");
                 if ((field.Element == FieldElement.Select ||
                     field.Element == FieldElement.RadioGroup ||
                     field.Element == FieldElement.CheckBoxGroup) &&
                    field.Options.Select(o => o.Value).Distinct().Count() != field.Options.Count)
                    throw new InvalidOperationException("Option values must be distinct.");
                if ((field.Element == FieldElement.CheckBoxGroup ||
                     field.Element == FieldElement.RadioGroup) &&
                    (field.Options.Count == 0 || field.Options.Any(o => string.IsNullOrEmpty(o.Value) ||
                        o.Value.Contains(","))))
                    throw new InvalidOperationException("Choice options need nonempty, comma-free values.");
                if (field.Element == FieldElement.CheckBoxGroup &&
                    field.Validators.Any(v => v.Kind != ValidatorKind.Required &&
                        v.Kind != ValidatorKind.OneOf))
                    throw new InvalidOperationException("Checkbox groups support Required and OneOf validators.");
            }
        }

        private void CheckScope(ActionScope scope)
        {
            if (scope == null) throw new InvalidOperationException("Action scope is required.");
            if (scope.Include != null && scope.Exclude != null)
                throw new InvalidOperationException("Include and Exclude cannot both be set.");
            foreach (var name in (scope.Include ?? new string[0]).Concat(scope.Exclude ?? new string[0]))
                if (!Actions.Any(a => a.Name == name)) throw new InvalidOperationException("Unknown action: " + name);
        }

        private static void CheckOrders(IEnumerable<int> orders)
        {
            if (orders.Distinct().Count() != orders.Count())
                throw new InvalidOperationException("Orders must be unique within a field and category.");
        }

        // Raw strings come from the MVC value provider, not an already-converted model.
        public FormResult Validate(string action, Func<string, string> read, ModelStateDictionary state = null)
        {
            if (read == null) throw new ArgumentNullException(nameof(read));
            return ValidateValues(action, name => new[] { read(name) }, state);
        }

        // Use Request.Form.GetValues for checkbox groups; an unchecked checkbox
        // posts no value. Do not rely on MVC's Boolean binder for this field.
        public FormResult ValidateValues(string action, Func<string, string[]> read,
            ModelStateDictionary state = null)
        {
            if (read == null) throw new ArgumentNullException(nameof(read));
            CheckConfiguration();
            Action(action);
            var result = new FormResult();
            var posted = Fields.ToDictionary(f => f.Name,
                f => f.Scope.Applies(action) ? read(f.Name) ?? new string[0] : new string[0]);
            var raw = Fields.ToDictionary(f => f.Name, f =>
                f.Element == FieldElement.CheckBoxGroup
                    ? string.Join(",", posted[f.Name]) : posted[f.Name].FirstOrDefault() ?? "");
            var normalized = Fields.ToDictionary(f => f.Name,
                f => f.Scope.Applies(action) ? Normalize(f, raw[f.Name]) : "");
            foreach (var field in Fields.Where(f => f.Scope.Applies(action)))
            {
                var text = normalized[field.Name];
                var selections = posted[field.Name];
                var minimum = field.Validators.FirstOrDefault(v =>
                    v.Kind == ValidatorKind.MinValue && v.Scope.Applies(action))?.Minimum;
                object converted = null;
                var conversionAttempted = false;
                string error = null;
                if (field.Element != FieldElement.CheckBoxGroup && selections.Length > 1)
                    error = field.Converter.ErrorMessage;
                if (error == null && field.Element == FieldElement.CheckBoxGroup &&
                    selections.Length != selections.Distinct(StringComparer.Ordinal).Count())
                    error = "Choose each option only once.";
                if (error == null && (field.Element == FieldElement.CheckBoxGroup ||
                                      field.Element == FieldElement.RadioGroup) &&
                    selections.Any(value => value == null || !field.Options.Any(o => o.Value == value)))
                    error = field.Validators.FirstOrDefault(v =>
                        v.Kind == ValidatorKind.OneOf && v.Scope.Applies(action))?.Message ??
                        "Choose an offered option.";
                foreach (var validator in field.Validators.Where(v => v.Scope.Applies(action)).OrderBy(v => v.Order))
                {
                    if (error != null) break;
                    if (IsTyped(validator))
                    {
                        if (!conversionAttempted)
                        {
                            conversionAttempted = true;
                            if (!TryConvert(field.Converter.Type, text, out converted))
                            {
                                error = validator.Kind == ValidatorKind.IsNumber
                                    ? validator.Message : field.Converter.ErrorMessage;
                                break;
                            }
                        }
                    }
                    if (!Passes(validator, field, text, normalized, converted, minimum,
                        selections))
                    {
                        error = validator.Message;
                        break;
                    }
                }
                if (error == null)
                {
                    if (!conversionAttempted && !TryConvert(field.Converter.Type, text, out converted))
                        error = field.Converter.ErrorMessage;
                    else if (field.Converter.Type == FieldValueType.String) converted = text;
                }
                if (error != null)
                {
                    result.Errors.Add(field.Name, error);
                    state?.AddModelError(field.Name, error);
                }
                else result.Values.Add(field.Name, field.Element == FieldElement.CheckBoxGroup
                    ? (object)selections : converted);
            }
            return result;
        }

        private static string Normalize(FormField field, string raw)
        {
            var text = field.Converter.Type == FieldValueType.String ? raw : raw.Trim();
            return field.Normalizer == null ? text : field.Normalizer.Apply(text);
        }

        private static bool IsTyped(FormValidator validator) =>
            validator.Kind == ValidatorKind.MinValue ||
            validator.Kind == ValidatorKind.MaxValue ||
            validator.Kind == ValidatorKind.Step ||
            validator.Kind == ValidatorKind.IsNumber;

        private static bool Passes(FormValidator rule, FormField field, string text,
            IDictionary<string, string> form, object converted, decimal? minimum,
            string[] selections)
        {
            switch (rule.Kind)
            {
                case ValidatorKind.Required: return field.Element == FieldElement.CheckBox
                    ? text == "true" : field.Element == FieldElement.CheckBoxGroup
                        ? selections.Length > 0 : text.Length != 0;
                case ValidatorKind.MinLength: return text.Length == 0 || text.Length >= rule.Length;
                case ValidatorKind.MaxLength: return text.Length <= rule.Length;
                case ValidatorKind.Pattern: return text.Length == 0 ||
                    System.Text.RegularExpressions.Regex.IsMatch(text, rule.Pattern);
                case ValidatorKind.OneOf: return field.Element == FieldElement.CheckBoxGroup
                    ? selections.All(value => field.Options.Any(o => o.Value == value))
                    : text.Length == 0 || field.Options.Any(o => o.Value == text);
                case ValidatorKind.MatchesField: return text == form[rule.OtherField];
                case ValidatorKind.MinValue: return converted == null ||
                    MeetsMinimum(converted, rule.Minimum.Value);
                case ValidatorKind.MaxValue: return converted == null ||
                    MeetsMaximum(converted, rule.Maximum.Value);
                case ValidatorKind.Step: return converted == null ||
                    MatchesStep(text, minimum ?? 0m, rule.StepSize.Value);
                case ValidatorKind.IsNumber: return true;
                case ValidatorKind.Custom: return rule.CustomCheck(text, form);
                default: throw new InvalidOperationException("Unknown validator: " + rule.Kind);
            }
        }

        private static bool MeetsMinimum(object value, decimal minimum)
        {
            if (value is float || value is double)
                return Convert.ToDouble(value, CultureInfo.InvariantCulture) >= (double)minimum;
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture) >= minimum;
        }

        private static bool MeetsMaximum(object value, decimal maximum)
        {
            if (value is float || value is double)
                return Convert.ToDouble(value, CultureInfo.InvariantCulture) <= (double)maximum;
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture) <= maximum;
        }

        // As with <input type="number">, the minimum is the step base; without one
        // the base is zero. Compare decimal text exactly to avoid binary rounding
        // rejecting values such as 0.3 with a 0.1 step.
        private static bool MatchesStep(string text, decimal baseValue, decimal step)
        {
            const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
            if (decimal.TryParse(text, styles, CultureInfo.InvariantCulture, out var decimalValue))
            {
                if (decimalValue == baseValue) return true;
                try { return (decimalValue - baseValue) % step == 0m; }
                catch (OverflowException) { return false; }
            }

            // A finite Single/Double beyond decimal's range cannot be aligned
            // reliably using a decimal step; reject rather than guess.
            return false;
        }

        private static bool TryConvert(FieldValueType converter, string text, out object value)
        {
            value = null;
            if (converter == FieldValueType.Boolean)
            {
                if (text == "true") { value = true; return true; }
                if (text == "false" || text.Length == 0) { value = false; return true; }
                return false;
            }
            if (text.Length == 0 || converter == FieldValueType.String)
            {
                value = converter == FieldValueType.String ? text : null;
                return true;
            }
            const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
            if (converter == FieldValueType.Int32 && int.TryParse(text, NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var i)) { value = i; return true; }
            if (converter == FieldValueType.Decimal && decimal.TryParse(text, styles,
                CultureInfo.InvariantCulture, out var d)) { value = d; return true; }
            if (converter == FieldValueType.Single && float.TryParse(text, styles,
                CultureInfo.InvariantCulture, out var f) && !float.IsInfinity(f) && !float.IsNaN(f))
                { value = f; return true; }
            if (converter == FieldValueType.Double && double.TryParse(text, styles,
                CultureInfo.InvariantCulture, out var x) && !double.IsInfinity(x) && !double.IsNaN(x))
                { value = x; return true; }
            return false;
        }

        public string ClientConfiguration()
        {
            CheckConfiguration();
            return new JavaScriptSerializer().Serialize(new
            {
                actions = Actions.Select(a => new { name = a.Name, controller = a.Controller,
                    endpoint = a.Endpoint }).ToArray(),
                fields = Fields.Select(f => new {
                    name = f.Name, converter = f.Converter.Type.ToString(),
                    conversionMessage = f.Converter.ErrorMessage,
                    normalizer = f.Normalizer == null ? null : new {
                        kind = f.Normalizer.Kind.ToString(), ruleId = f.Normalizer.RuleId
                    },
                    formatter = f.Formatter == null ? null : new { ruleId = f.Formatter.RuleId },
                    scope = f.Scope,
                    options = f.Options.Select(o => o.Value).ToArray(),
                    blockers = f.Blockers.OrderBy(b => b.Order).Select(b => new {
                        kind = b.Kind.ToString(), order = b.Order, maximumLength = b.MaximumLength,
                        prefixPattern = b.PrefixPattern, ruleId = b.RuleId
                    }).ToArray(),
                    validators = f.Validators.OrderBy(v => v.Order).Select(v => new {
                        kind = v.Kind.ToString(), order = v.Order, message = v.Message, scope = v.Scope,
                        length = v.Length, pattern = v.Pattern, otherField = v.OtherField,
                        ruleId = v.RuleId, minimum = v.Minimum, maximum = v.Maximum,
                        stepSize = v.StepSize
                    }).ToArray()
                }).ToArray()
            });
        }
    }
}