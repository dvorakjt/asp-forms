using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Web.Mvc;
using System.Web.Script.Serialization;

namespace FormsSandbox.Prototypes.Expression
{
    public enum FieldValueType { Text, Int32 }
    public enum ValidatorType { Required, IsInt, MaxLength, Range, Custom, NonZero }
    public enum TransformType { Trim, RejectZero }

    public sealed class ConverterDefinition
    {
        public FieldValueType To { get; set; }
        public string ErrorMessage { get; set; } // Optional: a fallback is used if conversion fails.
    }

    public sealed class TransformerDefinition
    {
        public TransformType Type { get; set; }
        public int Order { get; set; }
        public string ErrorMessage { get; set; } // Optional: used by an implicit guard if it fails.
    }

    public sealed class TextViewDefinition
    {
        public TransformType? BuiltIn { get; set; }
        public Expression<Func<string, string>> Custom { get; set; }

        public static TextViewDefinition Trim() => new TextViewDefinition { BuiltIn = TransformType.Trim };
        public static TextViewDefinition From(Expression<Func<string, string>> expression) =>
            new TextViewDefinition { Custom = expression ?? throw new ArgumentNullException(nameof(expression)) };
    }

    public sealed class ValidatorDefinition
    {
        public ValidatorType Type { get; set; }
        public int Order { get; set; }
        public string ErrorMessage { get; set; }
        public string ConversionErrorMessage { get; set; }
        public TextViewDefinition ViewTransform { get; set; }
        public FieldValueType? ConvertTo { get; set; }
        public int? Maximum { get; set; }
        public bool BlockInput { get; set; }
        public Expression<Func<int, int, bool>> Predicate { get; set; }
    }

    public sealed class FieldDefinition
    {
        public ConverterDefinition Converter { get; set; }
        public IList<ValidatorDefinition> Validators { get; } = new List<ValidatorDefinition>();
        public IList<TransformerDefinition> Transformers { get; } = new List<TransformerDefinition>();
    }

    public sealed class FieldResult
    {
        public string Raw { get; set; }
        public string Text { get; set; }
        public int? Value { get; set; }
        public string Error { get; set; }
    }

    public static class FieldPipeline
    {
        private const string DefaultConversionError = "Enter a valid value.";
        private const string DefaultTransformError = "Could not transform the value.";

        // A reusable view transform does not mutate Raw; a configured field transformer
        // runs only after validation and conversion. Both use the same Trim definition.
        public static string ApplyTextTransform(TransformType type, string text)
        {
            if (type != TransformType.Trim) throw new NotSupportedException("Not a text transform: " + type);
            return text.Trim(' ', '\t', '\r', '\n');
        }

        private static string ApplyView(TextViewDefinition view, string raw)
        {
            if (view == null) return raw;
            return view.Custom != null ? view.Custom.Compile()(raw) :
                ApplyTextTransform(view.BuiltIn.Value, raw);
        }

        public static IList<ValidatorDefinition> EffectiveValidators(FieldDefinition field)
        {
            if (field == null || field.Converter == null) throw new ArgumentNullException(nameof(field));
            var ordered = field.Validators.OrderBy(v => v.Order).ToList();
            var result = new List<ValidatorDefinition>();
            foreach (var validator in ordered)
            {
                if (validator.ConvertTo == FieldValueType.Int32 &&
                    !result.Any(v => v.Type == ValidatorType.IsInt))
                {
                    result.Add(new ValidatorDefinition { Type = ValidatorType.IsInt, Order = validator.Order,
                        ErrorMessage = validator.ConversionErrorMessage ?? field.Converter.ErrorMessage ??
                                       DefaultConversionError });
                }
                result.Add(validator);
            }
            if (field.Converter.To == FieldValueType.Int32 &&
                !result.Any(v => v.Type == ValidatorType.IsInt))
                result.Add(new ValidatorDefinition { Type = ValidatorType.IsInt, Order = int.MaxValue - 1,
                    ErrorMessage = field.Converter.ErrorMessage ?? DefaultConversionError });
            // A fallible transformer contributes a check before the conversion phase.
            // An explicit guard can be placed earlier to choose its own error message.
            foreach (var transform in field.Transformers.OrderBy(t => t.Order))
            {
                if (transform.Type == TransformType.RejectZero &&
                    !result.Any(v => v.Type == ValidatorType.NonZero))
                {
                    result.Add(new ValidatorDefinition { Type = ValidatorType.NonZero,
                        Order = int.MaxValue, ConvertTo = FieldValueType.Int32,
                        ErrorMessage = transform.ErrorMessage ?? DefaultTransformError });
                }
            }
            return result.OrderBy(v => v.Order).ToList();
        }

        public static FieldResult Evaluate(FieldDefinition field, string raw, int limit)
        {
            var result = new FieldResult { Raw = raw ?? "", Text = raw ?? "" };
            foreach (var validator in EffectiveValidators(field))
            {
                var view = ApplyView(validator.ViewTransform, result.Raw);
                if (validator.Type == ValidatorType.Required && view.Length == 0 ||
                    validator.Type == ValidatorType.MaxLength && view.Length > validator.Maximum.GetValueOrDefault())
                {
                    result.Error = validator.ErrorMessage;
                    return result;
                }
                if (validator.Type == ValidatorType.IsInt || validator.ConvertTo == FieldValueType.Int32)
                {
                    if (view.Trim(' ', '\t', '\r', '\n').Length == 0 &&
                        validator.Type != ValidatorType.IsInt) continue;
                    if (!TryConvertInt(view, out var number))
                    {
                        result.Error = validator.ErrorMessage ?? DefaultConversionError;
                        return result;
                    }
                    if (validator.Type == ValidatorType.Range && number > validator.Maximum.GetValueOrDefault() ||
                        validator.Type == ValidatorType.NonZero && number == 0 ||
                        validator.Type == ValidatorType.Custom && !validator.Predicate.Compile()(number, limit))
                    {
                        result.Error = validator.ErrorMessage;
                        return result;
                    }
                }
            }

            // Conversion is a separate, bounded phase; optional messages fall back safely.
            if (field.Converter.To == FieldValueType.Int32 && result.Raw.Length != 0)
            {
                if (!TryConvertInt(result.Raw, out var number))
                {
                    result.Error = field.Converter.ErrorMessage ?? DefaultConversionError;
                    return result;
                }
                result.Value = number;
            }
            foreach (var transform in field.Transformers.OrderBy(t => t.Order))
            {
                if (transform.Type == TransformType.Trim && field.Converter.To == FieldValueType.Text)
                    result.Text = ApplyTextTransform(transform.Type, result.Text);
                else if (transform.Type == TransformType.RejectZero && result.Value == 0)
                {
                    result.Error = transform.ErrorMessage ?? DefaultTransformError;
                    return result;
                }
            }
            return result;
        }

        public static void AddError(ModelStateDictionary state, string key, FieldResult result)
        {
            if (result.Error != null) state.AddModelError(key, result.Error);
        }

        public static string ManifestJson(FieldDefinition field)
        {
            var manifest = new
            {
                converter = new { to = field.Converter.To.ToString(), message = field.Converter.ErrorMessage },
                validators = EffectiveValidators(field).Select(v => new
                {
                    kind = v.Type.ToString(), order = v.Order, message = v.ErrorMessage,
                    conversionMessage = v.ConversionErrorMessage,
                    viewTransform = v.ViewTransform == null ? null : new {
                        kind = v.ViewTransform.Custom == null ? "builtIn" : "custom",
                        builtIn = v.ViewTransform.BuiltIn?.ToString(),
                        expression = v.ViewTransform.Custom == null ? null : ToTextAst(v.ViewTransform.Custom.Body)
                    }, convertTo = v.ConvertTo?.ToString(),
                    maximum = v.Maximum, blockInput = v.BlockInput,
                    predicate = v.Predicate == null ? null : ToAst(v.Predicate.Body)
                }).ToArray(),
                transformers = field.Transformers.OrderBy(t => t.Order)
                    .Select(t => new { kind = t.Type.ToString(), message = t.ErrorMessage }).ToArray()
            };
            return new JavaScriptSerializer().Serialize(manifest);
        }

        private static bool TryConvertInt(string raw, out int value)
        {
            return int.TryParse(raw.Trim(' ', '\t', '\r', '\n'), NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out value);
        }

        private static object ToAst(System.Linq.Expressions.Expression expression)
        {
            if (expression is BinaryExpression conjunction && conjunction.NodeType == ExpressionType.AndAlso)
                return new object[] { "and", ToAst(conjunction.Left), ToAst(conjunction.Right) };
            if (expression is BinaryExpression binary && binary.NodeType == ExpressionType.GreaterThan)
                return new object[] { "gt", ToAst(binary.Left), ToAst(binary.Right) };
            if (expression is ParameterExpression parameter)
                return new object[] { parameter.Name == "value" ? "value" : "limit" };
            if (expression is ConstantExpression constant && constant.Type == typeof(int))
                return new object[] { "int", constant.Value };
            throw new NotSupportedException("Unsupported custom expression: " + expression);
        }

        private static object ToTextAst(System.Linq.Expressions.Expression expression)
        {
            if (expression is MethodCallExpression call && call.Method.Name == nameof(string.Replace) &&
                call.Object != null && call.Arguments.Count == 2 &&
                call.Arguments.All(a => a is ConstantExpression c && c.Type == typeof(string)))
                return new object[] { "replace", ToTextAst(call.Object), ToTextAst(call.Arguments[0]),
                    ToTextAst(call.Arguments[1]) };
            if (expression is ParameterExpression) return new object[] { "raw" };
            if (expression is ConstantExpression constant && constant.Type == typeof(string))
                return new object[] { "string", constant.Value };
            throw new NotSupportedException("Unsupported text view: " + expression);
        }
    }

    // Sample configuration, not a public-library API commitment.
    public static class RevisionField
    {
        public static FieldDefinition Create(bool explicitInt = false, bool withRange = true)
        {
            var field = new FieldDefinition
            {
                Converter = new ConverterDefinition { To = FieldValueType.Int32 },
            };
            field.Validators.Add(new ValidatorDefinition { Type = ValidatorType.Required, Order = 10,
                ViewTransform = TextViewDefinition.Trim(), ErrorMessage = "Enter a revision." });
            field.Validators.Add(new ValidatorDefinition { Type = ValidatorType.MaxLength, Order = 20,
                Maximum = 12, BlockInput = true, ErrorMessage = "Revision is too long." });
            if (explicitInt) field.Validators.Add(new ValidatorDefinition { Type = ValidatorType.IsInt,
                Order = 25, ErrorMessage = "Use digits for the revision." });
            if (withRange) field.Validators.Add(new ValidatorDefinition { Type = ValidatorType.Range,
                Order = 30, Maximum = 5, ConvertTo = FieldValueType.Int32,
                ErrorMessage = "Revision must be 5 or less.",
                ConversionErrorMessage = "Enter a whole-number revision." });
            field.Validators.Add(new ValidatorDefinition { Type = ValidatorType.Custom,
                Order = 40, ConvertTo = FieldValueType.Int32, ViewTransform = TextViewDefinition.Trim(),
                ErrorMessage = "Revision and limit must be positive.",
                Predicate = (value, limit) => value > 0 && limit > 0 });
            return field;
        }
    }
}