using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Mvc;
using FormsSandbox.Examples;
using FormsSandbox.Forms;
using FormsSandbox.Models;

namespace FormsSandbox.Examples.Tests
{
    internal static class Program
    {
        private static void Main()
        {
            var form = DocumentFormDefinition.Create();
            var amount = form.Fields[4];
            Assert(amount.Blockers[0].Allows("12.5", new Dictionary<string, string>()));
            Assert(!amount.Blockers[0].Allows("12..5", new Dictionary<string, string>()));
            Assert(amount.Blockers[1].Allows("12.5", new Dictionary<string, string>()));
            Assert(!amount.Blockers[1].Allows("12 5", new Dictionary<string, string>()));

            var length = new InputBlocker { Kind = InputBlockerKind.MaxLength, MaximumLength = 3 };
            Assert(length.Allows("123", new Dictionary<string, string>()));
            Assert(!length.Allows("1234", new Dictionary<string, string>()));

            var config = form.ClientConfiguration();
            Assert(config.Contains("\"kind\":\"Pattern\""));
            Assert(config.Contains("\"kind\":\"Custom\""));
            Assert(config.Contains("\"kind\":\"MaxLength\""));
            Assert(config.Contains("\"kind\":\"MinValue\""));
            Assert(config.Contains("\"kind\":\"MaxValue\""));
            Assert(config.Contains("\"kind\":\"Step\""));
            Assert(config.Contains("\"stepSize\":0.01"));
            Assert(config.Contains("\"converter\":\"Decimal\""));
            Assert(config.Contains("\"normalizer\":{\"kind\":\"Trim\""));
            Assert(config.Contains("\"formatter\":null"));
            Assert(!config.Contains("\"trim\":true"));
            Assert(form.Fields[0].Name == nameof(DocumentForm.Title));
            var trimmed = ValidPost("0.01");
            trimmed["Title"] = new[] { "  A title  " };
            Assert((string)form.ValidateValues("Submit", name => trimmed[name]).Values["Title"] == "A title");
            var draftOnly = form.ValidateValues("SaveDraft", name =>
                name == "Title" ? new[] { "  Draft title  " } : null);
            Assert(draftOnly.IsValid && draftOnly.Values.Count == 4 &&
                (string)draftOnly.Values["Title"] == "Draft title");
            var phoneInput = ValidPost("0.01");
            phoneInput["Phone"] = new[] { "(415) 555-0123" };
            var phoneResult = form.ValidateValues("Submit", name => phoneInput[name]);
            Assert(phoneResult.IsValid && (string)phoneResult.Values["Phone"] == "4155550123");
            Assert(DocumentFormRules.FormatPhone("4155550123") == "415-555-0123");
            phoneInput["Phone"] = new[] { "555" };
            Assert(form.ValidateValues("Submit", name => phoneInput[name])
                .Errors["Phone"] == "Enter a ten-digit phone number.");
            var missingSubmit = form.ValidateValues("Submit", name =>
                name == "Title" ? new[] { "Draft title" } : null);
            Assert(!missingSubmit.IsValid && missingSubmit.Errors.ContainsKey("Summary") &&
                missingSubmit.Errors.ContainsKey("Amount") &&
                missingSubmit.Errors.ContainsKey("AcceptTerms"));
            var sameText = ValidPost("0.01");
            sameText["Summary"] = new[] { "A title" };
            var sameTextState = new System.Web.Mvc.ModelStateDictionary();
            var sameTextResult = form.ValidateValues("Submit", name => sameText[name], sameTextState);
            Assert(sameTextResult.Errors["Summary"] == "Use a summary different from the title." &&
                sameTextState["Summary"].Errors[0].ErrorMessage == sameTextResult.Errors["Summary"]);
            var forged = ValidPost("0.01");
            forged["Category"] = new[] { "unlisted" };
            Assert(form.ValidateValues("Submit", name => forged[name]).Errors["Category"] ==
                "Choose one of the offered categories.");

            var fieldDefinitions = new FormField {
                Name = "Title",
                Normalizer = new TextNormalizer { Kind = TextNormalizerKind.Trim },
                Converter = new FieldConverter { Type = FieldValueType.String },
                Formatter = new TextFormatter { RuleId = "Echo",
                    Format = text => text },
                Validators = new[] { new FormValidator { Kind = ValidatorKind.Required,
                    Order = 10, Message = "Enter a title." } },
                Blockers = new[] { new InputBlocker { Kind = InputBlockerKind.MaxLength,
                    Order = 10, MaximumLength = 100 } },
                Options = new[] { new FormOption { Value = "x", Label = "X" } }
            };
            Assert(fieldDefinitions.Normalizer.Apply("  hi  ") == "hi" &&
                fieldDefinitions.Formatter.Preview("hi") == "hi" &&
                fieldDefinitions.Validators.Count == 1 && fieldDefinitions.Blockers.Count == 1 &&
                fieldDefinitions.Options.Count == 1);
            fieldDefinitions.Validators = new[] {
                new FormValidator { Kind = ValidatorKind.Required, Order = 10, Message = "Required" },
                new FormValidator { Kind = ValidatorKind.MaxLength, Order = 20, Length = 80,
                    Message = "Too long" }
            };
            Assert(fieldDefinitions.Validators.Count == 2);

            var wrongProperty = DocumentFormDefinition.Create();
            wrongProperty.Fields[0].Name = "DoesNotExist";
            ExpectInvalid(wrongProperty);
            var wrongShape = DocumentFormDefinition.Create();
            wrongShape.Fields[6].Element = FieldElement.Input;
            ExpectInvalid(wrongShape);
            try {
                form.Field((DocumentForm model) => model.Title.Length);
                throw new Exception("A nested model property must not be accepted.");
            } catch (ArgumentException) { }

            var container = new TestViewDataContainer();
            var viewContext = new ViewContext { HttpContext = new TestHttpContext(),
                ViewData = container.ViewData,
                TempData = new TempDataDictionary(), Writer = new StringWriter() };
            var html = new HtmlHelper(viewContext, container);
            var titleMarkup = html.InputGroup(form.Fields[0], "Visible title",
                groupClass: "title-group", inputClass: "title-input").ToHtmlString();
            Assert(titleMarkup.Contains("Visible title"));
            Assert(titleMarkup.Contains("title-group"));
            Assert(titleMarkup.Contains("title-input"));
            viewContext.ViewData.ModelState.AddModelError("Title", "Enter a title.");
            var invalidTitleMarkup = html.InputGroup(form.Fields[0], "Visible title").ToHtmlString();
            Assert(invalidTitleMarkup.Contains("Enter a title."));
            var categoryMarkup = html.InputGroup(form.Fields[3], "Visible category").ToHtmlString();
            Assert(categoryMarkup.Contains("news") && categoryMarkup.Contains("guide"));
            var termsMarkup = html.InputGroup(form.Fields[5], "Accept terms").ToHtmlString();
            Assert(termsMarkup.Contains("type=\"checkbox\"") && termsMarkup.Contains("value=\"true\""));
            var topicsMarkup = html.InputGroup(form.Fields[6], "Topics").ToHtmlString();
            Assert(topicsMarkup.Contains("<fieldset") && topicsMarkup.Contains("<legend") &&
                topicsMarkup.Contains("science") && topicsMarkup.Contains("arts"));
            var radio = new FormField { Name = "Channel", Element = FieldElement.RadioGroup };
            radio.Options.Add(new FormOption { Value = "mail", Label = "Postal mail" });
            radio.Options.Add(new FormOption { Value = "email", Label = "Email" });
            var radioMarkup = html.InputGroup(radio, "Preferred channel").ToHtmlString();
            Assert(radioMarkup.Contains("type=\"radio\"") &&
                radioMarkup.Contains("Postal mail") && radioMarkup.Contains("<legend"));
            var emailMarkup = html.InputGroup(form.Fields[0], "Email", inputType: TextInputType.Email)
                .ToHtmlString();
            Assert(emailMarkup.Contains("type=\"email\""));
            var numberMarkup = html.InputGroup(form.Fields[4], "Amount",
                inputType: TextInputType.Number).ToHtmlString();
            Assert(numberMarkup.Contains("type=\"number\"") && numberMarkup.Contains("step=\"any\""));
            Assert(topicsMarkup.Contains("hidden=\"hidden\"") &&
                !topicsMarkup.Contains("aria-describedby"));
            var submittedView = new TestViewDataContainer();
            submittedView.ViewData.ModelState.AddModelError("Topics", "Choose at least one topic.");
            var submittedContext = new ViewContext { HttpContext = new TestHttpContext(),
                ViewData = submittedView.ViewData, TempData = new TempDataDictionary(),
                Writer = new StringWriter() };
            var submittedHtml = new HtmlHelper(submittedContext, submittedView);
            var errorMarkup = submittedHtml.InputGroup(form.Fields[6], "Topics").ToHtmlString();
            Assert(errorMarkup.Contains("form-field--invalid") &&
                errorMarkup.Contains("form-field--error-visible") &&
                errorMarkup.Contains("aria-describedby") &&
                !errorMarkup.Contains("hidden=\"hidden\""));
            var uncheckedMarkup = submittedHtml.InputGroup(form.Fields[5], "Accept terms").ToHtmlString();
            Assert(!uncheckedMarkup.Contains("checked=\"checked\""));
            submittedView.ViewData.ModelState.SetModelValue("Topics",
                new ValueProviderResult(new[] { "arts" }, "arts", System.Globalization.CultureInfo.InvariantCulture));
            var selectedTopicsMarkup = submittedHtml.InputGroup(form.Fields[6], "Topics").ToHtmlString();
            Assert(selectedTopicsMarkup.Contains("value=\"arts\" checked=\"checked\"") ||
                selectedTopicsMarkup.Contains("checked=\"checked\""));

            CheckAmount(form, "0", "Amount must be at least $0.01.");
            CheckAmount(form, "0.01", null);
            CheckAmount(form, "10000", null);
            CheckAmount(form, "10000.01", "Amount must not exceed $10,000.");
            CheckAmount(form, "0.03", null);
            CheckAmount(form, "0.015", "Enter an amount in whole cents.");

            var floating = DocumentFormDefinition.Create();
            floating.Fields[4].Converter.Type = FieldValueType.Double;
            CheckAmount(floating, "3000000000000000000000000000000000000000000000",
                "Amount must not exceed $10,000.");

            var minimumBased = DocumentFormDefinition.Create();
            minimumBased.Fields[4].Validators[2].Minimum = 0.2m;
            minimumBased.Fields[4].Validators[4].StepSize = 2m;
            CheckAmount(minimumBased, "0.2", null);
            CheckAmount(minimumBased, "2.2", null);
            CheckAmount(minimumBased, "1.2", "Enter an amount in whole cents.");
            CheckAmount(minimumBased, "0", "Amount must be at least $0.01.");

            var noMinimum = DocumentFormDefinition.Create();
            noMinimum.Fields[4].Validators.RemoveAt(2);
            noMinimum.Fields[4].Validators[3].StepSize = 0.1m;
            CheckAmount(noMinimum, "0.3", null);
            CheckAmount(noMinimum, "0.35", "Enter an amount in whole cents.");

            var scopedMinimum = DocumentFormDefinition.Create();
            scopedMinimum.Fields[4].Scope = new ActionScope();
            scopedMinimum.Fields[4].Validators[2].Scope = new ActionScope { Include = new[] { "Submit" } };
            scopedMinimum.Fields[4].Validators[4].StepSize = 2m;
            CheckAmount(scopedMinimum, "2.01", null);
            CheckAmountForAction(scopedMinimum, "SaveDraft", "2.01", "Enter an amount in whole cents.");
            CheckAmountForAction(scopedMinimum, "SaveDraft", "2", null);

            var invalidStep = DocumentFormDefinition.Create();
            invalidStep.Fields[4].Validators[4].StepSize = 0m;
            ExpectInvalid(invalidStep);
            invalidStep.Fields[4].Validators[4].StepSize = null;
            ExpectInvalid(invalidStep);

            var ambiguousMinimum = DocumentFormDefinition.Create();
            ambiguousMinimum.Fields[4].Validators.Add(new FormValidator {
                Kind = ValidatorKind.MinValue, Minimum = 1m, Order = 60, Message = "Too small." });
            ExpectInvalid(ambiguousMinimum);

            var conflictingFieldScope = DocumentFormDefinition.Create();
            conflictingFieldScope.Fields[0].Scope = new ActionScope {
                Include = new[] { "Submit" }, Exclude = new string[0] };
            ExpectInvalid(conflictingFieldScope);

            var conflictingValidatorScope = DocumentFormDefinition.Create();
            conflictingValidatorScope.Fields[0].Validators[0].Scope = new ActionScope {
                Include = new[] { "Submit" }, Exclude = new[] { "SaveDraft" } };
            ExpectInvalid(conflictingValidatorScope);

            var booleanNumeric = DocumentFormDefinition.Create();
            booleanNumeric.Fields[5].Validators.Add(new FormValidator {
                Kind = ValidatorKind.MinValue, Minimum = 1m, Order = 20, Message = "Invalid." });
            ExpectInvalid(booleanNumeric);

            Assert(new ActionScope().Applies("SaveDraft"));
            Assert(new ActionScope { Include = new[] { "Submit" } }.Applies("Submit"));
            Assert(!new ActionScope { Include = new[] { "Submit" } }.Applies("SaveDraft"));
            Assert(!new ActionScope { Exclude = new[] { "Submit" } }.Applies("Submit"));

            var uncheckedValues = ValidPost("0.01");
            uncheckedValues.Remove("AcceptTerms");
            var uncheckedResult = form.ValidateValues("Submit", name =>
                uncheckedValues.TryGetValue(name, out var selected) ? selected : null);
            Assert(uncheckedResult.Errors["AcceptTerms"] == "Accept the terms before submitting.");
            var badOptions = ValidPost("0.01");
            badOptions["Topics"] = new[] { "science", "unlisted" };
            var badOptionsResult = form.ValidateValues("Submit", name => badOptions[name]);
            Assert(badOptionsResult.Errors["Topics"] == "Choose an offered topic.");
            var goodOptionsResult = form.ValidateValues("Submit", name => ValidPost("0.01")[name]);
            Assert(goodOptionsResult.IsValid && (bool)goodOptionsResult.Values["AcceptTerms"] &&
                ((string[])goodOptionsResult.Values["Topics"]).Length == 2);
            var radioForm = new FormDefinition();
            radioForm.Actions.Add(new FormAction { Name = "Submit", Controller = "Document",
                Endpoint = "Submit" });
            radioForm.Fields.Add(radio);
            Assert(radioForm.ValidateValues("Submit", name => new[] { "email" }).IsValid);
            Assert(radioForm.ValidateValues("Submit", name => new[] { "not-listed" })
                .Errors.ContainsKey("Channel"));
            var duplicateOptions = ValidPost("0.01");
            duplicateOptions["Topics"] = new[] { "science", "science" };
            Assert(form.ValidateValues("Submit", name => duplicateOptions[name]).Errors.ContainsKey("Topics"));
            var malformedTerms = ValidPost("0.01");
            malformedTerms["AcceptTerms"] = new[] { "maybe" };
            Assert(form.ValidateValues("Submit", name => malformedTerms[name]).Errors.ContainsKey("AcceptTerms"));

            var missingBound = DocumentFormDefinition.Create();
            missingBound.Fields[4].Validators[3].Maximum = null;
            ExpectInvalid(missingBound);

            var bad = DocumentFormDefinition.Create();
            bad.Fields[0].Blockers.Add(new InputBlocker { Kind = InputBlockerKind.Custom, Order = 10 });
            ExpectInvalid(bad);
            bad.Fields[0].Blockers.Clear();
            bad.Fields[0].Blockers.Add(new InputBlocker { Kind = (InputBlockerKind)99 });
            ExpectInvalid(bad);

            Console.WriteLine("Form scopes, input blockers, and numeric bounds/steps passed.");
        }

        private static void CheckAmount(FormDefinition form, string amount, string expectedError)
        {
            CheckAmountForAction(form, "Submit", amount, expectedError);
        }

        private static void CheckAmountForAction(FormDefinition form, string action,
            string amount, string expectedError)
        {
            var values = ValidPost(amount);
            var result = form.ValidateValues(action, name => values[name]);
            if (expectedError == null)
                Assert(result.IsValid);
            else
                Assert(result.Errors.TryGetValue("Amount", out var error) && error == expectedError);
        }

        private static Dictionary<string, string[]> ValidPost(string amount)
        {
            return new Dictionary<string, string[]> {
                { "Title", new[] { "A title" } },
                { "Summary", new[] { "A different summary" } },
                { "Body", new[] { "A document" } },
                { "Category", new[] { "news" } },
                { "Amount", new[] { amount } },
                { "AcceptTerms", new[] { "true" } },
                { "Topics", new[] { "science", "arts" } },
                { "Phone", new[] { "" } }
            };
        }

        private static void Assert(bool condition)
        {
            if (!condition) throw new Exception("Form configuration test failed.");
        }

        private static void ExpectInvalid(FormDefinition form)
        {
            try { form.CheckConfiguration(); }
            catch (InvalidOperationException) { return; }
            throw new Exception("Expected configuration to be rejected.");
        }
    }
}