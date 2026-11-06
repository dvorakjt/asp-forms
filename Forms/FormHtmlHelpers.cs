using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;

namespace FormsSandbox.Forms
{
    public static class FormHtmlHelpers
    {
        public static MvcHtmlString ActionButton(this HtmlHelper html, FormDefinition form,
            string actionName, string label, string cssClass = null)
        {
            if (html == null) throw new ArgumentNullException(nameof(html));
            if (form == null) throw new ArgumentNullException(nameof(form));
            var action = form.Action(actionName);
            var url = new UrlHelper(html.ViewContext.RequestContext).Action(action.Endpoint,
                action.Controller);
            if (url == null) throw new InvalidOperationException("The action has no MVC route.");
            var button = new TagBuilder("button");
            button.Attributes["type"] = "submit";
            button.Attributes["data-form-action"] = action.Name;
            button.Attributes["formaction"] = url;
            AddClasses(button, cssClass);
            button.SetInnerText(label);
            return MvcHtmlString.Create(button.ToString());
        }

        // Labels and static classes belong in the view. The renderer owns state.
        public static MvcHtmlString InputGroup(
            this HtmlHelper html,
            FormField field,
            string label,
            string groupClass = null,
            string labelClass = null,
            string inputClass = null,
            string errorClass = null,
            ErrorRevealPolicy reveal = ErrorRevealPolicy.AfterBlurOrSubmit,
            TextInputType inputType = TextInputType.Text)
        {
            if (html == null) throw new ArgumentNullException(nameof(html));
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (string.IsNullOrEmpty(field.Name)) throw new ArgumentException("The field needs a name.", nameof(field));
            if (!Enum.IsDefined(typeof(ErrorRevealPolicy), reveal)) throw new ArgumentOutOfRangeException(nameof(reveal));
            if (!Enum.IsDefined(typeof(TextInputType), inputType) ||
                (inputType != TextInputType.Text && field.Element != FieldElement.Input))
                throw new ArgumentOutOfRangeException(nameof(inputType));

            var error = html.ViewData.ModelState[field.Name]?.Errors.FirstOrDefault()?.ErrorMessage;
            var invalid = !string.IsNullOrEmpty(error);
            // ModelState errors are already returned from the server: reveal them
            // even if the client-side policy would ordinarily wait for blur.
            var visible = invalid;
            var group = new TagBuilder(field.Element == FieldElement.CheckBoxGroup ||
                field.Element == FieldElement.RadioGroup ? "fieldset" : "div");
            AddClasses(group, groupClass, invalid ? "form-field--invalid" : null);
            if (visible) group.AddCssClass("form-field--error-visible");
            group.Attributes["data-form-field"] = field.Name;
            group.Attributes["data-error-reveal"] = reveal.ToString();

            var errorId = TagBuilder.CreateSanitizedId(field.Name + "_error");
            var inputClasses = Combine(inputClass, invalid ? "form-input--invalid" : null);
            var attributes = new Dictionary<string, object> { { "class", inputClasses },
                { "data-form-control", field.Name } };
            if (visible)
            {
                attributes["aria-invalid"] = "true";
                attributes["aria-describedby"] = errorId;
            }

            string labelMarkup;
            if (field.Element == FieldElement.CheckBoxGroup || field.Element == FieldElement.RadioGroup)
            {
                var legend = new TagBuilder("legend");
                AddClasses(legend, labelClass);
                legend.SetInnerText(label);
                labelMarkup = legend.ToString();
            }
            else labelMarkup = html.Label(field.Name, label,
                new { @class = labelClass }).ToHtmlString();

            string inputMarkup;
            switch (field.Element)
            {
                case FieldElement.Input:
                    attributes["type"] = inputType.ToString().ToLowerInvariant();
                    // A native number input defaults to step=1. The form's Step
                    // validator, not the browser default, owns allowed increments.
                    if (inputType == TextInputType.Number) attributes["step"] = "any";
                    inputMarkup = html.TextBox(field.Name, null, attributes).ToHtmlString();
                    break;
                case FieldElement.TextArea:
                    attributes["rows"] = 4;
                    inputMarkup = html.TextArea(field.Name, null, attributes).ToHtmlString();
                    break;
                case FieldElement.Select:
                    inputMarkup = html.DropDownList(field.Name,
                        field.Options.Select(option => new SelectListItem
                        {
                            Text = option.Label,
                            Value = option.Value
                        }), "Choose an option", attributes).ToHtmlString();
                    break;
                case FieldElement.CheckBox:
                    inputMarkup = Checkbox(field.Name, "true", IsChecked(html, field.Name, "true"),
                        inputClasses, visible ? errorId : null);
                    break;
                case FieldElement.CheckBoxGroup:
                case FieldElement.RadioGroup:
                    inputMarkup = string.Concat(field.Options.Select((option, index) => {
                        var optionId = TagBuilder.CreateSanitizedId(field.Name + "_" + index);
                        var choice = Choice(field.Element == FieldElement.RadioGroup ? "radio" : "checkbox",
                            field.Name, option.Value,
                            IsChecked(html, field.Name, option.Value), inputClasses,
                            visible ? errorId : null, optionId);
                        var optionLabel = new TagBuilder("label");
                        optionLabel.Attributes["for"] = optionId;
                        optionLabel.SetInnerText(option.Label);
                        return choice + optionLabel.ToString();
                    }));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(field), "Unsupported input element.");
            }

            var message = new TagBuilder("span");
            message.Attributes["id"] = errorId;
            message.Attributes["data-form-error"] = field.Name;
            AddClasses(message, errorClass, "form-error");
            if (invalid) message.SetInnerText(error);
            if (!visible) message.Attributes["hidden"] = "hidden";
            group.InnerHtml = labelMarkup + inputMarkup + message.ToString();
            return MvcHtmlString.Create(group.ToString());
        }

        private static bool IsChecked(HtmlHelper html, string name, string value)
        {
            var attempted = html.ViewData.ModelState[name]?.Value?.RawValue;
            if (attempted is string[] posted) return posted.Contains(value, StringComparer.Ordinal);
            if (attempted is string single) return single == value;
            var modelValue = html.ViewData.Eval(name);
            if (modelValue is IEnumerable<string> selected) return selected.Contains(value);
            if (modelValue is bool flag) return flag && value == "true";
            return modelValue?.ToString() == value;
        }

        private static string Checkbox(string name, string value, bool selected,
            string cssClass, string errorId, string id = null) =>
            Choice("checkbox", name, value, selected, cssClass, errorId, id);

        private static string Choice(string type, string name, string value, bool selected,
            string cssClass, string errorId, string id = null)
        {
            var input = new TagBuilder("input");
            input.Attributes["type"] = type;
            input.Attributes["name"] = name;
            input.Attributes["id"] = id ?? TagBuilder.CreateSanitizedId(name);
            input.Attributes["value"] = value;
            input.Attributes["data-form-control"] = name;
            AddClasses(input, cssClass);
            if (selected) input.Attributes["checked"] = "checked";
            if (errorId != null)
            {
                input.Attributes["aria-invalid"] = "true";
                input.Attributes["aria-describedby"] = errorId;
            }
            return input.ToString(TagRenderMode.SelfClosing);
        }

        private static string Combine(string first, string second) =>
            string.Join(" ", new[] { first, second }.Where(value => !string.IsNullOrWhiteSpace(value)));

        private static void AddClasses(TagBuilder tag, params string[] classes)
        {
            var combined = Combine(classes.ElementAtOrDefault(0), classes.ElementAtOrDefault(1));
            if (!string.IsNullOrEmpty(combined)) tag.AddCssClass(combined);
        }
    }
}