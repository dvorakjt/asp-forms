using System;

namespace FormsSandbox.Forms2.Examples
{
    public static class EnrollmentDefinition
    {
        public static FormDefinition<EnrollmentModel> Create()
        {
            var form = new FormDefinition<EnrollmentModel> { DefaultAction = "Submit" };
            form.Actions.Add(new FormAction { Name = "SaveDraft", Controller = "Enrollment",
                Endpoint = "SaveDraft" });
            form.Actions.Add(new FormAction { Name = "Submit", Controller = "Enrollment",
                Endpoint = "Submit" });

            var plan = form.Field(model => model.Plan);
            plan.NormalizeValue = EnrollmentRules.NormalizePlan;
            plan.Validators.Add(new FieldValidator<string> { Order = 10,
                Check = EnrollmentRules.HasPlan, Message = "Choose a plan." });

            var choice = form.Field(model => model.CardChoice);
            choice.Scope = new ActionScope { Include = new[] { "Submit" },
                Condition = EnrollmentRules.IsPaid };
            choice.Validators.Add(new FieldValidator<string> { Order = 10,
                Check = EnrollmentRules.HasChoice, Message = "Choose a card option." });

            var saved = form.Field(model => model.SavedCard);
            saved.Scope = new ActionScope { Include = new[] { "Submit" },
                Condition = EnrollmentRules.UseSavedCard };
            saved.Input = new InputPolicy { Allows = EnrollmentRules.AllowsSavedCardEdit,
                Format = EnrollmentRules.FormatSavedCard };
            saved.Validators.Add(new FieldValidator<string> { Order = 10,
                Check = EnrollmentRules.HasSavedCard, Message = "Choose an available saved card." });
            saved.Validators.Add(new FieldValidator<string> { Order = 20,
                CrossField = true, CheckOnServerAsync = EnrollmentRules.IsSavedCardAvailable,
                Message = "The saved card is unavailable." });

            var month = form.Field(model => model.ExpiryMonth);
            month.Scope = new ActionScope { Include = new[] { "Submit" },
                Condition = EnrollmentRules.UseNewCard };
            month.Validators.Add(new FieldValidator<int?> { Order = 10,
                Check = EnrollmentRules.MonthInRange, Message = "Choose a valid month." });
            month.Validators.Add(new FieldValidator<int?> { Order = 20,
                CrossField = true, Check = EnrollmentRules.ExpiryIsCoherent,
                Message = "The card has expired." });
            month.Validators.Add(new FieldValidator<int?> { Order = 30,
                CrossField = true, CheckOnServerAsync = EnrollmentRules.IsMonthEligibleAsync,
                Message = "The month cannot be used." });

            var year = form.Field(model => model.ExpiryYear);
            year.Scope = new ActionScope { Include = new[] { "Submit" },
                Condition = EnrollmentRules.UseNewCard };
            year.Validators.Add(new FieldValidator<int?> { Order = 10,
                Check = EnrollmentRules.YearInRange, Message = "Choose a valid year." });
            year.Validators.Add(new FieldValidator<int?> { Order = 20,
                CrossField = true, CheckOnServerAsync = EnrollmentRules.IsYearEligibleAsync,
                Message = "The year cannot be used." });

            var amount = form.Field(model => model.Amount);
            amount.Scope = new ActionScope { Include = new[] { "Submit" },
                Condition = EnrollmentRules.IsPaid };
            amount.Validators.Add(new FieldValidator<decimal?> { Order = 10,
                Check = EnrollmentRules.AmountIsPositive, Message = "Enter a positive amount." });
            form.CheckConfiguration();
            return form;
        }
    }
}