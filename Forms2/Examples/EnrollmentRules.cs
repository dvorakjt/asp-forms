using System;
using System.Threading.Tasks;
using FormsSandbox.Forms2.Shared;

namespace FormsSandbox.Forms2.Examples
{
    public static class EnrollmentRules
    {
        public static string NormalizePlan(string plan) => ValueRules.Trim(plan);

        public static bool IsPaid(FormSnapshot form) =>
            form.RequireValid<string>("Plan").Value == "paid";

        public static bool UseSavedCard(FormSnapshot form) =>
            form.RequireValid<string>("CardChoice").Value == "saved";

        public static bool UseNewCard(FormSnapshot form) =>
            form.RequireValid<string>("CardChoice").Value == "new";

        public static bool HasPlan(FieldState<string> field, FormSnapshot form) =>
            field.IsPresent && (field.Value == "free" || field.Value == "paid");

        public static bool HasChoice(FieldState<string> field, FormSnapshot form) =>
            field.IsPresent && (field.Value == "saved" || field.Value == "new");

        public static bool HasSavedCard(FieldState<string> field, FormSnapshot form) =>
            field.IsPresent && field.Value.Length >= 5 &&
            field.Value.Substring(0, 5) == "card-";

        public static bool AllowsSavedCardEdit(string candidate)
        {
            if (candidate.Length > 8) return false;
            foreach (var character in candidate)
                if (!(character >= 'a' && character <= 'z') &&
                    !(character >= 'A' && character <= 'Z') &&
                    !(character >= '0' && character <= '9') && character != '-') return false;
            return true;
        }

        public static string FormatSavedCard(string text) => text.Replace("CARD", "card");

        public static bool MonthInRange(FieldState<int?> field, FormSnapshot form) =>
            field.IsPresent && field.Value >= 1 && field.Value <= 12;

        public static bool YearInRange(FieldState<int?> field, FormSnapshot form) =>
            field.IsPresent && field.Value >= 2020 && field.Value <= 2100;

        public static bool ExpiryIsCoherent(FieldState<int?> month, FormSnapshot form)
        {
            var year = form.RequireValid<int?>("ExpiryYear").Value;
            return month.Value.HasValue && year.HasValue &&
                (year.Value > 2026 || year.Value == 2026 && month.Value >= 11);
        }

        public static bool AmountIsPositive(FieldState<decimal?> field, FormSnapshot form) =>
            field.IsPresent && field.Value > 0m;

        public static Task<bool> IsSavedCardAvailable(FieldState<string> field, FormSnapshot form) =>
            Task.FromResult(field.Value == "card-123");

        public static Task<bool> IsYearEligibleAsync(FieldState<int?> field, FormSnapshot form) =>
            Task.FromResult(field.Value.HasValue && field.Value.Value >= 2020);

        public static Task<bool> IsMonthEligibleAsync(FieldState<int?> field, FormSnapshot form) =>
            Task.FromResult(form.RequireValid<int?>("ExpiryYear").Value.HasValue &&
                field.Value.HasValue);

        public static Task<bool> IsYearAfterMonthAsync(FieldState<int?> field, FormSnapshot form) =>
            Task.FromResult(form.RequireValid<int?>("ExpiryMonth").Value.HasValue);
    }
}