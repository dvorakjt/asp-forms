using FormsSandbox.Forms;
using FormsSandbox.Models;

namespace FormsSandbox.Examples
{
    public static class DocumentFormDefinition
    {
        // The same server-owned options will feed both select rendering and validation.
        public static FormDefinition Create()
        {
            var form = FormDefinition.For<DocumentForm>();
            form.Actions.Add(new FormAction { Name = "SaveDraft", Controller = "Document",
                Endpoint = "SaveDraft" });
            form.Actions.Add(new FormAction { Name = "Submit", Controller = "Document",
                Endpoint = "Submit" });

            var title = form.Field((DocumentForm model) => model.Title);
            title.Normalizer = new TextNormalizer { Kind = TextNormalizerKind.Trim };
            title.Validators.Add(new FormValidator { Kind = ValidatorKind.Required, Order = 10,
                Message = "Enter a title." });
            title.Validators.Add(new FormValidator { Kind = ValidatorKind.MaxLength, Order = 20, Length = 80,
                Message = "Keep the title under 81 characters." });

            var summary = form.Field((DocumentForm model) => model.Summary);
            summary.Element = FieldElement.TextArea;
            summary.Normalizer = new TextNormalizer { Kind = TextNormalizerKind.Trim };
            summary.Validators.Add(new FormValidator { Kind = ValidatorKind.Required, Order = 10,
                Scope = Only("Submit"), Message = "Enter a summary before submitting." });
            summary.Validators.Add(new FormValidator { Kind = ValidatorKind.Custom, Order = 20,
                Scope = Only("Submit"), OtherField = nameof(DocumentForm.Title),
                RuleId = "SummaryDiffersFromTitle", Message = "Use a summary different from the title.",
                CustomCheck = (text, values) => DocumentFormRules.SummaryDiffersFromTitle(
                    text, values[nameof(DocumentForm.Title)]) });

            var body = form.Field((DocumentForm model) => model.Body);
            body.Element = FieldElement.TextArea;
            body.Scope = Except("SaveDraft");
            body.Validators.Add(new FormValidator { Kind = ValidatorKind.Required, Order = 10,
                Message = "Enter the document text before submitting." });

            var category = form.Field((DocumentForm model) => model.Category);
            category.Element = FieldElement.Select;
            category.Options = new[] {
                new FormOption { Value = "news", Label = "News" },
                new FormOption { Value = "guide", Label = "Guide" }
            };
            category.Validators.Add(new FormValidator { Kind = ValidatorKind.Required, Order = 10,
                Scope = Only("Submit"), Message = "Choose a category before submitting." });
            category.Validators.Add(new FormValidator { Kind = ValidatorKind.OneOf, Order = 20,
                Scope = Only("Submit"), Message = "Choose one of the offered categories." });

            var amount = form.Field((DocumentForm model) => model.Amount);
            amount.Converter = new FieldConverter { Type = FieldValueType.Decimal,
                ErrorMessage = "Enter a valid amount." };
            amount.Scope = Only("Submit");
            amount.Blockers.Add(new InputBlocker { Kind = InputBlockerKind.Pattern, Order = 10,
                PrefixPattern = @"^[+-]?[0-9]*(\.[0-9]*)?$" });
            amount.Blockers.Add(new InputBlocker { Kind = InputBlockerKind.Custom, Order = 20,
                RuleId = "AmountHasNoEmbeddedSpaces",
                CustomCheck = (candidate, values) => DocumentFormRules.AmountHasNoEmbeddedSpaces(candidate) });
            amount.Validators.Add(new FormValidator { Kind = ValidatorKind.Required, Order = 10,
                Message = "Enter an amount." });
            amount.Validators.Add(new FormValidator { Kind = ValidatorKind.IsNumber, Order = 20,
                Message = "Enter a valid amount." });
            amount.Validators.Add(new FormValidator { Kind = ValidatorKind.MinValue, Order = 30,
                Minimum = 0.01m,
                Message = "Amount must be at least $0.01." });
            amount.Validators.Add(new FormValidator { Kind = ValidatorKind.MaxValue, Order = 40,
                Maximum = 10000m, Message = "Amount must not exceed $10,000." });
            amount.Validators.Add(new FormValidator { Kind = ValidatorKind.Step, Order = 50,
                StepSize = 0.01m, Message = "Enter an amount in whole cents." });

            var terms = form.Field((DocumentForm model) => model.AcceptTerms);
            terms.Element = FieldElement.CheckBox;
            terms.Converter = new FieldConverter { Type = FieldValueType.Boolean };
            terms.Scope = Only("Submit");
            terms.Validators.Add(new FormValidator { Kind = ValidatorKind.Required, Order = 10,
                Message = "Accept the terms before submitting." });

            var topics = form.Field((DocumentForm model) => model.Topics);
            topics.Element = FieldElement.CheckBoxGroup;
            topics.Scope = Only("Submit");
            topics.Options = new[] {
                new FormOption { Value = "science", Label = "Science" },
                new FormOption { Value = "arts", Label = "Arts" }
            };
            topics.Validators.Add(new FormValidator { Kind = ValidatorKind.Required, Order = 10,
                Message = "Choose at least one topic." });
            topics.Validators.Add(new FormValidator { Kind = ValidatorKind.OneOf, Order = 20,
                Message = "Choose an offered topic." });

            var phone = form.Field((DocumentForm model) => model.Phone);
            phone.Normalizer = new TextNormalizer { Kind = TextNormalizerKind.Custom,
                RuleId = "PhoneDigits", CustomNormalize = DocumentFormRules.PhoneDigits };
            phone.Formatter = new TextFormatter { RuleId = "FormatPhone",
                Format = DocumentFormRules.FormatPhone };
            phone.Blockers.Add(new InputBlocker { Kind = InputBlockerKind.MaxLength,
                Order = 10, MaximumLength = 12 });
            phone.Validators.Add(new FormValidator { Kind = ValidatorKind.Pattern, Order = 10,
                Pattern = @"^[0-9]{10}$", Scope = Only("Submit"),
                Message = "Enter a ten-digit phone number." });

            form.CheckConfiguration();
            return form;
        }

        private static ActionScope Only(string action) => new ActionScope { Include = new[] { action } };
        private static ActionScope Except(string action) => new ActionScope { Exclude = new[] { action } };
    }
}