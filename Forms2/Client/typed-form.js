(function () {
  "use strict";

  var shared = window.FormsSandbox.Forms2;
  var builtins = shared.Shared.ValueRules;
  var rules = shared.Examples.EnrollmentRules;

  function parse(raw, type) {
    var text = builtins.Trim(raw);
    if (type === "String") return { ok: true, value: raw };
    if (!text) return { ok: true, value: null };
    if (type === "Int32") {
      var unsigned = /^[+-]/.test(text) ? text.slice(1) : text;
      var number = Number(text);
      return { ok: builtins.AsciiDigits(unsigned) && Number.isInteger(number) &&
        number >= -2147483648 && number <= 2147483647, value: number };
    }
    if (type === "Single" || type === "Double") {
      var floating = Number(text);
      if (type === "Single") floating = Math.fround(floating);
      return { ok: builtins.FloatingSyntax(text) && Number.isFinite(floating),
        value: floating };
    }
    if (type === "Decimal") {
      // BigInt bounds are used instead of Number's lossy decimal arithmetic.
      var match = /^([+-]?)(\d*)(?:\.(\d*))?$/.exec(text);
      if (!builtins.DecimalSyntax(text) || !match) return { ok: false };
      var digits = (match[2] || "0") + (match[3] || "");
      var magnitude = BigInt(digits);
      return { ok: magnitude <= 79228162514264337593543950335n,
        value: System.Decimal(text) };
    }
    return { ok: false };
  }

  function attach(form) {
    var config = JSON.parse(form.getAttribute("data-typed-form"));
    var action = config.defaultAction;
    var touched = new Set();
    var submitted = false;
    var serverErrors = {};
    var dependencies = {};
    var byName = Object.fromEntries(config.fields.map(function (f) { return [f.name, f]; }));
    form.querySelectorAll("[data-typed-field]").forEach(function (group) {
      var error = group.querySelector("[data-typed-error]");
      if (error && error.textContent.trim()) {
        serverErrors[group.getAttribute("data-typed-field")] = error.textContent.trim();
        submitted = true;
      }
    });

    function evaluate() {
      var states = {};
      config.fields.forEach(function (field) {
        var element = form.elements.namedItem(field.name);
        var raw = element?.value || "";
        var conversion = parse(raw, field.type);
        if (conversion.ok && field.normalize)
          conversion.value = rules[field.normalize](conversion.value);
        states[field.name] = { Name: field.name, Raw: raw, IsPresent: !!raw,
          ConversionSucceeded: conversion.ok, Value: conversion.value,
          IsIncluded: false, Error: conversion.ok ? null : "Invalid " + field.type + " value." };
      });
      var resolved = new Set();
      var visiting = new Set();
      var pendingLocal = new Set();
      var tracking = [];
      function record(key, fn) {
        var reads = new Set();
        tracking.push(reads);
        try { return fn(); }
        finally { tracking.pop(); dependencies[key] = Array.from(reads); }
      }
      var snapshot = {
        Inspect: function (type, name) {
          if (!states[name]) throw new Error("Unknown field " + name);
          tracking.forEach(function (set) { set.add(name); });
          resolve(name);
          return states[name];
        },
        RequireValid: function (type, name) {
          var state = this.Inspect(type, name);
          if (pendingLocal.has(name)) throw { deferred: true };
          if (!state.IsValid) throw { unavailable: true };
          return state;
        }
      };
      function scopeApplies(scope) {
        if (scope.include && !scope.include.includes(action)) return false;
        if (scope.exclude && scope.exclude.includes(action)) return false;
        return !scope.condition || rules[scope.condition](snapshot);
      }
      function check(field, cross) {
        var state = states[field.name];
        if (!state.IsValid) return;
        for (var validator of field.validators) {
          if (validator.crossField !== cross) continue;
          try {
            record("validate:" + field.name, function () {
              if (!scopeApplies(validator)) return;
              if (!rules[validator.method](state, snapshot))
                state.Error = validator.message;
            });
          } catch (error) { if (error.deferred) throw error;
            if (!error.unavailable) throw error; }
          state.IsValid = state.IsIncluded && state.ConversionSucceeded && !state.Error;
          if (state.Error) break;
        }
      }
      function resolve(name) {
        if (resolved.has(name)) return;
        if (visiting.has(name)) throw new Error("Participation cycle involving " + name);
        visiting.add(name);
        var field = byName[name];
        try {
          record("include:" + name, function () {
            try { states[name].IsIncluded = scopeApplies(field); }
            catch (error) { if (!error.unavailable) throw error; }
          });
        } finally { visiting.delete(name); }
        resolved.add(name);
        states[name].IsValid = states[name].IsIncluded && states[name].ConversionSucceeded &&
          !states[name].Error;
        if (states[name].IsValid) {
          if (field.validators.some(function (v) { return !v.crossField; }))
            pendingLocal.add(name);
          try { check(field, false); pendingLocal.delete(name); }
          catch (error) { if (!error.deferred) throw error; }
        }
      }
      config.fields.forEach(function (field) { resolve(field.name); });
      while (pendingLocal.size) {
        var progressed = false;
        config.fields.forEach(function (field) {
          if (!pendingLocal.has(field.name)) return;
          try { check(field, false); pendingLocal.delete(field.name); progressed = true; }
          catch (error) { if (!error.deferred) throw error; }
        });
        if (!progressed) throw new Error("Circular local validation prerequisites.");
      }
      config.fields.forEach(function (field) { check(field, true); });
      config.fields.forEach(function (field) {
        var state = states[field.name];
        var group = Array.from(form.querySelectorAll("[data-typed-field]"))
          .find(function (node) { return node.getAttribute("data-typed-field") === field.name; });
        if (!group) return;
        group.hidden = !state.IsIncluded;
        var error = group.querySelector("[data-typed-error]");
        var message = serverErrors[field.name] || state.Error;
        var show = !!message && (touched.has(field.name) || submitted);
        error.textContent = show ? message : "";
        error.hidden = !show;
        var input = form.elements.namedItem(field.name);
        if (show) { input.setAttribute("aria-invalid", "true");
          input.setAttribute("aria-describedby", error.id); }
        else { input.removeAttribute("aria-invalid"); input.removeAttribute("aria-describedby"); }
      });
      return states;
    }

    function formatInput(field, input) {
      if (!field.input?.format || typeof input.selectionStart !== "number") return;
      try {
        var oldPosition = input.selectionStart;
        var formatted = rules[field.input.format](input.value);
        if (typeof formatted !== "string") throw new Error("Formatter returned a non-string value.");
        if (formatted !== input.value) {
          input.value = formatted;
          input.setSelectionRange(Math.min(oldPosition, formatted.length),
            Math.min(oldPosition, formatted.length));
        }
      } catch (error) { console.error("Typed input formatter failed", error); }
    }

    form.addEventListener("beforeinput", function (event) {
      var field = byName[event.target.name];
      var input = event.target;
      if (!field?.input?.allow || event.isComposing ||
          (event.inputType || "").startsWith("delete") ||
          typeof input.selectionStart !== "number") return;
      var inserted = event.data;
      if (inserted === null && event.dataTransfer) inserted = event.dataTransfer.getData("text");
      if (inserted === null) return;
      var candidate = input.value.slice(0, input.selectionStart) + inserted +
        input.value.slice(input.selectionEnd);
      try { if (!rules[field.input.allow](candidate)) event.preventDefault(); }
      catch (error) { console.error("Typed input blocker failed", error); }
    });

    form.addEventListener("input", function (event) {
      if (event.target.name) {
        delete serverErrors[event.target.name];
        if (!event.isComposing) formatInput(byName[event.target.name], event.target);
        evaluate();
      }
    });
    form.addEventListener("change", function (event) {
      if (event.target.name) { delete serverErrors[event.target.name]; evaluate(); }
    });
    form.addEventListener("focusout", function (event) {
      if (event.target.name) { touched.add(event.target.name); evaluate(); }
    });
    form.addEventListener("compositionend", function (event) {
      if (event.target.name) { formatInput(byName[event.target.name], event.target); evaluate(); }
    });
    form.addEventListener("submit", function (event) {
      var submitter = event.submitter || document.activeElement;
      action = submitter?.getAttribute("data-form-action") || config.defaultAction;
      if (!config.actions.some(function (item) { return item.name === action; })) {
        event.preventDefault(); return;
      }
      submitted = true;
      try {
        var states = evaluate();
        var invalid = config.fields.map(function (f) { return states[f.name]; })
          .filter(function (state) { return state.IsIncluded && !state.IsValid; });
        if (invalid.length) {
          event.preventDefault();
          form.elements.namedItem(invalid[0].Name).focus();
        }
      } catch (error) { event.preventDefault(); console.error("Typed form failed", error); }
    });
    evaluate();
    return { setAction: function (name) {
      if (!config.actions.some(function (item) { return item.name === name; }))
        throw new Error("Unknown action " + name);
      action = name;
      evaluate();
    }, dependencies: function () { return dependencies; } };
  }

  window.TypedForms = { attach: attach };
  document.querySelectorAll("form[data-typed-form]").forEach(attach);
}());