(function () {
  "use strict";

  function applies(scope, action) {
    if (scope.Include !== null && scope.Include !== undefined)
      return scope.Include.includes(action);
    return !scope.Exclude || !scope.Exclude.includes(action);
  }

  function decimalParts(text) {
    var match = /^([+-]?)(\d*)(?:\.(\d*))?$/.exec(text);
    if (!match || !(match[2] || match[3])) return null;
    var fraction = match[3] || "";
    return {
      units: BigInt(
        (match[1] === "-" ? "-" : "") + (match[2] || "0") + fraction,
      ),
      scale: fraction.length,
    };
  }

  function compareDecimals(a, b) {
    var scale = Math.max(a.scale, b.scale);
    var left = a.units * 10n ** BigInt(scale - a.scale);
    var right = b.units * 10n ** BigInt(scale - b.scale);
    return left < right ? -1 : left > right ? 1 : 0;
  }

  function matchesStep(text, base, step) {
    var value = decimalParts(text);
    var origin = decimalParts(String(base));
    var increment = decimalParts(String(step));
    if (!value || !origin || !increment) return false;
    var scale = Math.max(value.scale, origin.scale, increment.scale);
    var factor = function (part) {
      return part.units * 10n ** BigInt(scale - part.scale);
    };
    return (factor(value) - factor(origin)) % factor(increment) === 0n;
  }

  function convert(text, kind) {
    if (kind === "String") return { valid: true, value: text };
    if (kind === "Boolean")
      return {
        valid: text === "true" || text === "false" || text === "",
        value: text === "true",
      };
    if (!text) return { valid: true, value: null };
    if (kind === "Int32") {
      var integer = /^[+-]?\d+$/.test(text) ? Number(text) : NaN;
      return {
        valid:
          Number.isInteger(integer) &&
          integer >= -2147483648 &&
          integer <= 2147483647,
        value: integer,
      };
    }
    var decimal = decimalParts(text);
    if (!decimal) return { valid: false };
    if (kind === "Decimal") {
      // .NET Decimal has 96 bits of significand and up to 28 decimal places.
      return {
        valid:
          decimal.scale <= 28 &&
          (decimal.units < 0n ? -decimal.units : decimal.units) <=
            79228162514264337593543950335n,
        value: decimal,
      };
    }
    var value = Number(text);
    if (kind === "Single") value = Math.fround(value);
    return { valid: Number.isFinite(value), value: value };
  }

  function attach(form) {
    var configuration = JSON.parse(form.getAttribute("data-form-config"));
    var state = window.FormState.forForm(form);
    var selectedAction = configuration.actions[0].name;
    var fields = configuration.fields;
    var byName = Object.fromEntries(
      fields.map(function (field) {
        return [field.name, field];
      }),
    );

    function controls(field) {
      return Array.from(form.elements).filter(function (element) {
        return (
          element.name === field.name &&
          element.hasAttribute("data-form-control")
        );
      });
    }

    function read(field) {
      var elements = controls(field);
      if (!elements.length) return "";
      if (elements[0].type === "checkbox" && elements.length === 1)
        return elements[0].checked ? "true" : "";
      if (elements[0].type === "checkbox")
        return elements
          .filter(function (element) {
            return element.checked;
          })
          .map(function (element) {
            return element.value;
          })
          .join(",");
      if (elements[0].type === "radio")
        return (
          elements.find(function (element) {
            return element.checked;
          })?.value || ""
        );
      return elements[0].value;
    }

    function normalized() {
      var values = {};
      fields.forEach(function (field) {
        if (!applies(field.scope, selectedAction)) {
          values[field.name] = "";
          return;
        }
        var text = read(field);
        if (field.converter !== "String") text = text.trim();
        if (field.normalizer) {
          if (field.normalizer.kind === "Trim") text = text.trim();
          else if (field.normalizer.kind === "Custom")
            text =
              window.FormRuleRegistry.normalizers[field.normalizer.method](
                text,
              );
          else throw new Error("Unknown normalizer: " + field.normalizer.kind);
        }
        values[field.name] = text;
      });
      return values;
    }

    function errorFor(field, values, action) {
      var text = values[field.name];
      var options = field.options;
      var selections = text ? text.split(",") : [];
      var group =
        controls(field).length > 1 && controls(field)[0].type === "checkbox";
      if (
        group &&
        selections.some(function (value) {
          return !options.includes(value);
        })
      )
        return (
          field.validators.find(function (rule) {
            return rule.kind === "OneOf" && applies(rule.scope, action);
          })?.message || "Choose an offered option."
        );
      var converted;
      var minimum =
        field.validators.find(function (rule) {
          return rule.kind === "MinValue" && applies(rule.scope, action);
        })?.minimum ?? 0;
      for (var rule of field.validators) {
        if (!applies(rule.scope, action)) continue;
        if (
          ["IsNumber", "MinValue", "MaxValue", "Step"].includes(rule.kind) &&
          !converted
        ) {
          converted = convert(text, field.converter);
          if (!converted.valid)
            return rule.kind === "IsNumber"
              ? rule.message
              : field.conversionMessage;
        }
        var invalid = false;
        switch (rule.kind) {
          case "Required":
            invalid = field.converter === "Boolean" ? text !== "true" : !text;
            break;
          case "MinLength":
            invalid = !!text && text.length < rule.length;
            break;
          case "MaxLength":
            invalid = text.length > rule.length;
            break;
          case "Pattern":
            invalid = !!text && !new RegExp(rule.pattern).test(text);
            break;
          case "OneOf":
            invalid = group
              ? selections.some(function (item) {
                  return !options.includes(item);
                })
              : !!text && !options.includes(text);
            break;
          case "MatchesField":
            invalid = text !== values[rule.otherField];
            break;
          case "MinValue":
            invalid =
              !!text &&
              (field.converter === "Decimal"
                ? compareDecimals(
                    converted.value,
                    decimalParts(String(rule.minimum)),
                  ) < 0
                : converted.value < Number(rule.minimum));
            break;
          case "MaxValue":
            invalid =
              !!text &&
              (field.converter === "Decimal"
                ? compareDecimals(
                    converted.value,
                    decimalParts(String(rule.maximum)),
                  ) > 0
                : converted.value > Number(rule.maximum));
            break;
          case "Step":
            invalid = !!text && !matchesStep(text, minimum, rule.stepSize);
            break;
          case "IsNumber":
            break;
          case "Custom":
            invalid = !window.FormRuleRegistry.validators[rule.method](
              text,
              values,
            );
            break;
          default:
            throw new Error("Unknown validator: " + rule.kind);
        }
        if (invalid) return rule.message;
      }
      if (!converted && !convert(text, field.converter).valid)
        return field.conversionMessage;
      return null;
    }

    function validateField(field, action, values) {
      if (!applies(field.scope, action)) {
        state.setError(field.name, null);
        return false;
      }
      var message = errorFor(field, values, action);
      state.setError(field.name, message);
      return !!message;
    }

    function validate(action) {
      selectedAction = action;
      var values = normalized();
      var invalid = [];
      fields.forEach(function (field) {
        if (validateField(field, action, values)) {
          var control = controls(field).find(function (node) {
            return node.offsetParent !== null;
          });
          if (control) invalid.push(control);
        }
      });
      if (invalid.length) {
        invalid.sort(function (a, b) {
          var x = a.getBoundingClientRect();
          var y = b.getBoundingClientRect();
          if (x.top < y.bottom && y.top < x.bottom)
            return x.left - y.left || x.top - y.top;
          return x.top - y.top;
        });
        invalid[0].focus();
      }
      return !invalid.length;
    }

    function formatField(field, element) {
      if (!field.formatter || typeof element.selectionStart !== "number")
        return;
      try {
        var position = element.selectionStart;
        var digitsBeforeCaret = element.value
          .slice(0, position)
          .replace(/[^0-9]/g, "").length;
        var formatted = window.FormRuleRegistry.formatters[
          field.formatter.method
        ](element.value);
        if (typeof formatted !== "string")
          throw new Error("Formatter returned a non-string value.");
        if (formatted === element.value) return;
        element.value = formatted;
        if (document.activeElement === element) {
          var next = 0;
          var seen = 0;
          while (next < formatted.length && seen < digitsBeforeCaret) {
            if (/[0-9]/.test(formatted.charAt(next))) seen++;
            next++;
          }
          element.setSelectionRange(next, next);
        }
      } catch (error) {
        console.error("Input formatter failed", error);
      }
    }

    form.addEventListener("input", function (event) {
      var field = byName[event.target.name];
      if (!field) return;
      if (!event.isComposing) formatField(field, event.target);
      try {
        var values = normalized();
        validateField(field, selectedAction, values);
        fields.forEach(function (dependent) {
          if (
            dependent !== field &&
            dependent.validators.some(function (rule) {
              return (
                rule.otherField === field.name &&
                applies(rule.scope, selectedAction)
              );
            })
          )
            validateField(dependent, selectedAction, values);
        });
      } catch (error) {
        console.error("Form validation failed", error);
      }
    });
    form.addEventListener("change", function (event) {
      if (byName[event.target.name])
        event.target.dispatchEvent(new Event("input", { bubbles: true }));
    });
    form.addEventListener("compositionend", function (event) {
      if (byName[event.target.name])
        event.target.dispatchEvent(new Event("input", { bubbles: true }));
    });
    form.addEventListener("focusout", function (event) {
      var field = byName[event.target.name];
      if (!field) return;
      try {
        validateField(field, selectedAction, normalized());
      } catch (error) {
        console.error("Form validation failed", error);
      }
    });
    form.addEventListener("beforeinput", function (event) {
      var field = byName[event.target.name];
      if (
        !field ||
        event.isComposing ||
        (event.inputType || "").startsWith("delete") ||
        typeof event.target.selectionStart !== "number"
      )
        return;
      var inserted = event.data;
      if (inserted === null && event.dataTransfer)
        inserted = event.dataTransfer.getData("text");
      if (inserted === null) return;
      var element = event.target;
      var candidate =
        element.value.slice(0, element.selectionStart) +
        inserted +
        element.value.slice(element.selectionEnd);
      for (var blocker of field.blockers) {
        try {
          var allowed =
            blocker.kind === "MaxLength"
              ? candidate.length <= blocker.maximumLength
              : blocker.kind === "Pattern"
                ? new RegExp(blocker.prefixPattern).test(candidate)
                : blocker.kind === "Custom"
                  ? window.FormRuleRegistry.blockers[blocker.method](
                      candidate,
                      normalized(),
                    )
                  : false;
          if (!allowed) {
            event.preventDefault();
            return;
          }
        } catch (error) {
          console.error("Input blocker failed", error);
          return;
        }
      }
    });
    form.addEventListener("submit", function (event) {
      var button = event.submitter || document.activeElement;
      var action = button?.getAttribute("data-form-action") || selectedAction;
      if (
        !configuration.actions.some(function (item) {
          return item.name === action;
        })
      ) {
        event.preventDefault();
        return;
      }
      selectedAction = action;
      state.markSubmitted();
      try {
        if (!validate(action)) event.preventDefault();
      } catch (error) {
        console.error("Form validation failed", error);
        event.preventDefault();
      }
    });

    return {
      submit: function (action) {
        var button = Array.from(
          form.querySelectorAll("button[data-form-action]"),
        ).find(function (item) {
          return item.getAttribute("data-form-action") === action;
        });
        if (!button) throw new Error("Unknown form action: " + action);
        form.requestSubmit(button);
      },
    };
  }

  var instances = new WeakMap();
  window.FormsDemo = {
    forForm: function (form) {
      if (!instances.has(form)) instances.set(form, attach(form));
      return instances.get(form);
    },
  };
  document
    .querySelectorAll("form[data-form-config]")
    .forEach(window.FormsDemo.forForm);
})();
