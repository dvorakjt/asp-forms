(function () {
  "use strict";
  // Registration maps stable IDs to the methods compiled from DocumentFormRules.cs.
  // It contains no second implementation of either rule.
  var rules = window.FormsSandbox.Examples.DocumentFormRules;
  window.FormRuleRegistry = {
    normalizers: { PhoneDigits: rules.PhoneDigits },
    formatters: { FormatPhone: rules.FormatPhone },
    validators: {
      SummaryDiffersFromTitle: function (summary, values) {
        return rules.SummaryDiffersFromTitle(summary, values.Title);
      },
    },
    blockers: {
      AmountHasNoEmbeddedSpaces: function (candidate) {
        return rules.AmountHasNoEmbeddedSpaces(candidate);
      },
    },
  };
})();
