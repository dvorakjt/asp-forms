(function () {
  "use strict";
  // Registration maps method names to code compiled from DocumentFormRules.cs.
  // It contains no second implementation of either rule.
  var rules = window.FormsSandbox.Examples.DocumentFormRules;
  window.FormRuleRegistry = {
    normalizers: { PhoneDigits: rules.PhoneDigits },
    formatters: { FormatPhone: rules.FormatPhone },
    validators: {
      SummaryDiffersFromTitleInForm: function (summary, values) {
        return rules.SummaryDiffersFromTitle(summary, values.Title);
      },
    },
    blockers: {
      AmountHasNoEmbeddedSpacesInForm: function (candidate) {
        return rules.AmountHasNoEmbeddedSpaces(candidate);
      },
    },
  };
})();
