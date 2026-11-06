using System;
using System.Globalization;
using FormsSandbox.Forms2.Shared;

namespace FormsSandbox.Forms2
{
    public sealed class FieldConversion<T>
    {
        public bool Succeeded { get; set; }
        public T Value { get; set; }
        public string Error { get; set; }
    }

    public static class FieldConversion
    {
        public static FieldConversion<T> Parse<T>(string raw)
        {
            var type = typeof(T);
            var nullable = Nullable.GetUnderlyingType(type);
            var actual = nullable ?? type;
            var text = ValueRules.Trim(raw);
            object value = null;
            var success = true;
            if (actual == typeof(string)) value = raw ?? "";
            else if (actual == typeof(string[]))
                throw new InvalidOperationException("Collection fields need a collection converter.");
            else if (actual == typeof(bool))
            {
                success = text == "" || text == "true" || text == "false";
                value = text == "true";
            }
            else if (text == "") success = nullable != null;
            else if (actual == typeof(int))
            {
                var unsigned = text[0] == '-' || text[0] == '+' ? text.Substring(1) : text;
                var number = 0;
                success = ValueRules.AsciiDigits(unsigned) &&
                    int.TryParse(text, NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out number);
                if (success) value = number;
            }
            else if (actual == typeof(decimal))
            {
                var number = 0m;
                success = ValueRules.DecimalSyntax(text) &&
                    decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out number);
                if (success) value = number;
            }
            else if (actual == typeof(float) || actual == typeof(double))
            {
                var number = 0d;
                success = ValueRules.FloatingSyntax(text) &&
                    double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out number) &&
                    !double.IsInfinity(number) && !double.IsNaN(number);
                if (success)
                {
                    value = actual == typeof(float) ? (object)(float)number : number;
                    if (actual == typeof(float) && float.IsInfinity((float)value)) success = false;
                }
            }
            else throw new InvalidOperationException("Unsupported model property type: " + type.Name);

            return new FieldConversion<T> {
                Succeeded = success,
                Value = success && value != null ? (T)value : default(T),
                Error = success ? null : "Enter a valid " + actual.Name + " value."
            };
        }
    }
}