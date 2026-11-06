using System;

namespace FormsSandbox.Prototypes.Transpose
{
    public sealed class Outcome
    {
        public string Raw { get; set; }
        public string Text { get; set; }
        public int? Value { get; set; }
        public string Error { get; set; }

        public string Snapshot()
        {
            return Raw + "|" + Text + "|" + (Value.HasValue ? Value.Value.ToString() : "null") +
                   "|" + (Error ?? "null");
        }
    }

    internal sealed class Step
    {
        public int Order;
        public bool IsTransform;
        public string Kind;
        public string Message;

        public Step(int order, bool isTransform, string kind, string message)
        {
            Order = order;
            IsTransform = isTransform;
            Kind = kind;
            Message = message;
        }
    }

    // The identical source file is compiled by the MVC project for server use and by
    // Transpose for browser use. This prototype does not modify the existing MVC actions.
    public static class SharedQuantity
    {
        // A single, statically authored C# lambda is compiled for both runtimes.
        private static readonly Func<Outcome, int, bool> WithinLimit =
            (field, limit) => field.Value.HasValue && field.Value.Value <= limit;

        public static readonly string[] Samples =
        {
            " 4 ", " 4 ", " ", " nope ", "0", "-1", "+2", "2147483648", "5"
        };

        public static readonly int[] Limits = { 5, 3, 5, 5, 5, 5, 5, 5, 5 };

        private static readonly Step[] Steps =
        {
            new Step(20, true, "trim", null),
            new Step(30, true, "integer", "Enter a whole number."),
            new Step(10, false, "required", "Enter a quantity."),
            new Step(40, false, "maximum", "Quantity exceeds the limit."),
            new Step(50, false, "positive", "Quantity must be positive.")
        };

        public static Outcome Evaluate(string raw, int limit)
        {
            var result = new Outcome { Raw = raw ?? "", Text = raw ?? "" };
            for (var pass = 0; pass < 2; pass++)
            {
                // This small spike uses numbered steps to make ordering observable.
                for (var order = 0; order <= 50; order++)
                {
                    foreach (var step in Steps)
                    {
                        if (step.IsTransform != (pass == 0) || step.Order != order)
                        {
                            continue;
                        }

                        if (step.Kind == "trim")
                        {
                            result.Text = result.Text.Trim(' ', '\t', '\r', '\n');
                        }
                        else if (step.Kind == "integer" && result.Text.Length != 0)
                        {
                            if (!int.TryParse(result.Text, out var value))
                            {
                                result.Error = step.Message;
                            }
                            else
                            {
                                result.Value = value;
                            }
                        }
                        else if (step.Kind == "required" && result.Text.Length == 0 ||
                                 step.Kind == "maximum" && result.Value.HasValue && !WithinLimit(result, limit) ||
                                 step.Kind == "positive" && result.Value.HasValue && result.Value.Value <= 0)
                        {
                            result.Error = step.Message;
                        }

                        if (result.Error != null)
                        {
                            return result;
                        }
                    }
                }
            }
            return result;
        }
    }
}