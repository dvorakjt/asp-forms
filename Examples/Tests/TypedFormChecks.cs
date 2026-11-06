using System;
using System.Collections.Generic;
using FormsSandbox.Forms2;
using FormsSandbox.Forms2.Examples;
using FormsSandbox.Forms2.Shared;

namespace FormsSandbox.Examples.Tests
{
    internal static class TypedFormChecks
    {
        private static Dictionary<string, string[]> Post(string plan, string choice,
            string month = "11", string year = "2026") => new Dictionary<string, string[]> {
                ["Plan"] = new[] { plan }, ["CardChoice"] = new[] { choice },
                ["SavedCard"] = new[] { "card-123" }, ["ExpiryMonth"] = new[] { month },
                ["ExpiryYear"] = new[] { year }, ["Amount"] = new[] { "15.00" }
            };

        public static void Run()
        {
            var form = EnrollmentDefinition.Create();
            var free = Post("free", "new");
            var draft = form.ValidateAsync("SaveDraft", name => free[name]).GetAwaiter().GetResult();
            Assert(draft.IsValid && !draft.Fields["CardChoice"].IsIncluded &&
                draft.Model.CardChoice == null);
            var result = form.ValidateAsync("Submit", name => free[name]).GetAwaiter().GetResult();
            Assert(result.IsValid && result.Model.Plan == "free" &&
                result.Model.CardChoice == null && result.Model.Amount == null &&
                !result.Fields["CardChoice"].IsIncluded);
            free["Plan"] = new[] { "\u0085 free \u0085" };
            result = form.ValidateAsync("Submit", name => free[name]).GetAwaiter().GetResult();
            Assert(result.IsValid && result.Model.Plan == "free" &&
                result.Fields["Plan"].Raw == "\u0085 free \u0085");

            var paid = Post("paid", "new", "0", "2026");
            result = form.ValidateAsync("Submit", name => paid[name]).GetAwaiter().GetResult();
            Assert(!result.IsValid && result.Fields["ExpiryMonth"].Error == "Choose a valid month.");
            Assert(result.Fields["ExpiryYear"].IsValid && !result.Fields["SavedCard"].IsIncluded);

            paid["ExpiryMonth"] = new[] { "10" };
            result = form.ValidateAsync("Submit", name => paid[name]).GetAwaiter().GetResult();
            Assert(result.Fields["ExpiryMonth"].Error == "The card has expired." &&
                Array.IndexOf(result.Dependencies["ExpiryMonth"], "ExpiryYear") >= 0);

            paid["ExpiryMonth"] = new[] { "11" };
            paid["Amount"] = new[] { "15.00" };
            result = form.ValidateAsync("Submit", name => paid[name]).GetAwaiter().GetResult();
            Assert(result.IsValid && result.Model.Amount == 15m && result.Model.ExpiryMonth == 11);

            paid["CardChoice"] = new[] { "saved" };
            result = form.ValidateAsync("Submit", name => paid[name]).GetAwaiter().GetResult();
            Assert(result.IsValid && !result.Fields["ExpiryMonth"].IsIncluded &&
                result.Model.ExpiryMonth == null && result.Model.SavedCard == "card-123");

            paid["SavedCard"] = new[] { "card-456" };
            result = form.ValidateAsync("Submit", name => paid[name]).GetAwaiter().GetResult();
            Assert(!result.IsValid && result.Fields["SavedCard"].Error == "The saved card is unavailable.");

            var invalidChoice = Post("paid", "unknown");
            result = form.ValidateAsync("Submit", name => invalidChoice[name]).GetAwaiter().GetResult();
            Assert(!result.IsValid && !result.Fields["SavedCard"].IsIncluded &&
                !result.Fields["ExpiryMonth"].IsIncluded);

            var badDecimal = FieldConversion.Parse<decimal?>("0.12345678901234567890123456789");
            Assert(!badDecimal.Succeeded);
            Assert(FieldConversion.Parse<double?>("0.12345678901234567890123456789").Succeeded);
            Assert(!FieldConversion.Parse<float?>("99999999999999999999999999999999999999999").Succeeded);
            Assert(ValueRules.Trim("\u0085 x \u0085") == "x");
            Assert(!ValueRules.AsciiDigits("\u0661"));

            var manifest = form.ClientConfiguration();
            Assert(manifest.Contains("\"defaultAction\":\"Submit\""));
            Assert(manifest.Contains("\"condition\":\"UseNewCard\""));
            Assert(manifest.Contains("\"allow\":\"AllowsSavedCardEdit\""));
            Assert(manifest.Contains("\"format\":\"FormatSavedCard\""));
            Assert(EnrollmentRules.AllowsSavedCardEdit("CARD-123") &&
                !EnrollmentRules.AllowsSavedCardEdit("card 123") &&
                EnrollmentRules.FormatSavedCard("CARD-123") == "card-123");

            var cycle = new FormDefinition<EnrollmentModel> { DefaultAction = "Submit" };
            cycle.Actions.Add(new FormAction { Name = "Submit", Controller = "Enrollment",
                Endpoint = "Submit" });
            cycle.Field(model => model.Plan).Scope = new ActionScope {
                Condition = EnrollmentRules.IsPaid
            };
            try
            {
                cycle.ValidateAsync("Submit", name => Post("paid", "new")[name])
                    .GetAwaiter().GetResult();
                throw new Exception("A participation cycle was not rejected.");
            }
            catch (InvalidOperationException exception)
            {
                Assert(exception.Message.Contains("Participation cycle"));
            }

            var asyncCycle = new FormDefinition<EnrollmentModel> { DefaultAction = "Submit" };
            asyncCycle.Actions.Add(new FormAction { Name = "Submit", Controller = "Enrollment",
                Endpoint = "Submit" });
            asyncCycle.Field(model => model.ExpiryMonth).Validators.Add(new FieldValidator<int?> {
                Order = 10, CrossField = true,
                CheckOnServerAsync = EnrollmentRules.IsMonthEligibleAsync, Message = "Month invalid." });
            asyncCycle.Field(model => model.ExpiryYear).Validators.Add(new FieldValidator<int?> {
                Order = 10, CrossField = true,
                CheckOnServerAsync = EnrollmentRules.IsYearAfterMonthAsync, Message = "Year invalid." });
            try
            {
                asyncCycle.ValidateAsync("Submit", name => Post("paid", "new")[name])
                    .GetAwaiter().GetResult();
                throw new Exception("An async validation cycle was not rejected.");
            }
            catch (InvalidOperationException exception)
            {
                Assert(exception.Message.Contains("Circular server-only async"));
            }
            Console.WriteLine("Typed enrollment, dependencies and parity fixtures passed.");
        }

        private static void Assert(bool condition)
        {
            if (!condition) throw new Exception("Typed enrollment test failed.");
        }
    }
}