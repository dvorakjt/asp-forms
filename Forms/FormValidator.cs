using System;
using System.Collections.Generic;

namespace FormsSandbox.Forms
{
    public sealed class FormValidator
    {
        public ValidatorKind Kind { get; set; }
        public int Order { get; set; }
        public string Message { get; set; }
        public ActionScope Scope { get; set; } = new ActionScope();
        public int? Length { get; set; }
        public string Pattern { get; set; }
        public string OtherField { get; set; }
        public string RuleId { get; set; }
        public Func<string, IDictionary<string, string>, bool> CustomCheck { get; set; }
        public decimal? Minimum { get; set; }
        public decimal? Maximum { get; set; }
        public decimal? StepSize { get; set; }
    }
}