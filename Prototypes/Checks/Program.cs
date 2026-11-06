using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using FormsSandbox.Prototypes.Expression;
using FormsSandbox.Prototypes.Transpose;

namespace FormsSandbox.Prototypes.Checks
{
    public static class Program
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        private static void Case(string raw, int limit, FieldDefinition field)
        {
            var result = FieldPipeline.Evaluate(field, raw, limit);
            var state = new ModelStateDictionary();
            FieldPipeline.AddError(state, "Revision", result);
            if (state.IsValid != (result.Error == null) ||
                result.Error != null && state["Revision"].Errors[0].ErrorMessage != result.Error)
                throw new Exception("ModelState error does not match the definition.");

            Console.WriteLine("CASE\t" + Json.Serialize(new {
                raw, limit, manifest = Json.DeserializeObject(FieldPipeline.ManifestJson(field)),
                result = new { raw = result.Raw, text = result.Text, value = result.Value, error = result.Error }
            }));
        }

        public static void Main()
        {
            foreach (var raw in SharedQuantity.Samples.Distinct()) Case(raw, 5, RevisionField.Create());
            Case(" 4 ", 3, RevisionField.Create());
            Case("1", 0, RevisionField.Create()); // The custom rule reads form state.
            Case("1234567890123", 5, RevisionField.Create());
            Case("nope", 5, RevisionField.Create());
            Case("nope", 5, RevisionField.Create(explicitInt: true));
            Case("nope", 5, RevisionField.Create(withRange: false));

            var guarded = RevisionField.Create(withRange: false);
            guarded.Validators.Clear();
            guarded.Transformers.Add(new TransformerDefinition {
                Type = TransformType.RejectZero, ErrorMessage = "Zero is not a valid revision." });
            Case("0", 5, guarded);
            Case("nope", 5, guarded);
            guarded.Validators.Add(new ValidatorDefinition { Type = ValidatorType.NonZero,
                Order = 5, ConvertTo = FieldValueType.Int32, ErrorMessage = "Use a nonzero revision." });
            Case("0", 5, guarded);

            var text = new FieldDefinition { Converter = new ConverterDefinition { To = FieldValueType.Text } };
            text.Validators.Add(new ValidatorDefinition { Type = ValidatorType.Required,
                Order = 10, ViewTransform = TextViewDefinition.Trim(), ErrorMessage = "Enter a title." });
            text.Transformers.Add(new TransformerDefinition { Type = TransformType.Trim, Order = 20 });
            Case("  Draft  ", 5, text);
            Case("   ", 5, text);

            var custom = RevisionField.Create(withRange: false);
            custom.Validators.Add(new ValidatorDefinition { Type = ValidatorType.IsInt,
                Order = 25, ViewTransform = TextViewDefinition.Trim(), ErrorMessage = "Enter an integer." });
            Case(" +2 ", 5, custom);

            var customView = RevisionField.Create(withRange: false);
            customView.Validators.Clear();
            customView.Validators.Add(new ValidatorDefinition { Type = ValidatorType.Custom,
                Order = 10, ConvertTo = FieldValueType.Int32,
                ViewTransform = TextViewDefinition.From(s => s.Replace("+", "")),
                Predicate = (value, limit) => value > 0 && limit > 0,
                ErrorMessage = "Revision and limit must be positive." });
            Case("+2", 5, customView);
        }
    }
}