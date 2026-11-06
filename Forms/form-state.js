(function () {
  "use strict";

  function attach(form) {
    var blurred = new WeakSet();
    var submitted = false;

    function reveal(group) {
      var error = group.querySelector("[data-form-error]");
      if (!error) return;
      var invalid = group.classList.contains("form-field--invalid");
      var policy = group.getAttribute("data-error-reveal");
      var visible =
        invalid &&
        (policy === "Immediately" ||
          submitted ||
          (policy === "AfterBlurOrSubmit" && blurred.has(group)) ||
          group.classList.contains("form-field--error-visible"));
      group.classList.toggle("form-field--error-visible", visible);
      error.hidden = !visible;
      group.querySelectorAll("[data-form-control]").forEach(function (control) {
        if (visible) {
          control.setAttribute("aria-invalid", "true");
          control.setAttribute("aria-describedby", error.id);
        } else {
          control.removeAttribute("aria-invalid");
          control.removeAttribute("aria-describedby");
        }
      });
    }

    form.addEventListener("focusout", function (event) {
      var group = event.target.closest("[data-form-field]");
      if (!group || !form.contains(group)) return;
      if (event.relatedTarget && group.contains(event.relatedTarget)) return;
      blurred.add(group);
      reveal(group);
    });
    form.addEventListener("submit", function () {
      submitted = true;
      form.querySelectorAll("[data-form-field]").forEach(reveal);
    });

    return {
      setError: function (fieldName, message) {
        var group = Array.from(form.querySelectorAll("[data-form-field]")).find(
          function (node) {
            return node.getAttribute("data-form-field") === fieldName;
          },
        );
        if (!group) throw new Error("Unknown field: " + fieldName);
        var error = group.querySelector("[data-form-error]");
        error.textContent = message || "";
        group.classList.toggle("form-field--invalid", Boolean(message));
        group.classList.toggle("form-field--valid", !message);
        group
          .querySelectorAll("[data-form-control]")
          .forEach(function (control) {
            control.classList.toggle("form-input--invalid", Boolean(message));
          });
        reveal(group);
      },
      markSubmitted: function () {
        submitted = true;
        form.querySelectorAll("[data-form-field]").forEach(reveal);
      },
    };
  }

  var controllers = new WeakMap();
  window.FormState = {
    forForm: function (form) {
      if (!controllers.has(form)) controllers.set(form, attach(form));
      return controllers.get(form);
    },
  };
  document
    .querySelectorAll("form[data-form-state]")
    .forEach(window.FormState.forForm);
})();
