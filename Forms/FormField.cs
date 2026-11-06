using System.Collections.Generic;

namespace FormsSandbox.Forms
{
    public sealed class FormField
    {
        public string Name { get; set; }
        public FieldElement Element { get; set; } = FieldElement.Input;
        public ActionScope Scope { get; set; } = new ActionScope();
        public FieldConverter Converter { get; set; } = new FieldConverter();
        public TextNormalizer Normalizer { get; set; }
        public TextFormatter Formatter { get; set; }
        public IList<FormValidator> Validators { get; set; } = new List<FormValidator>();
        public IList<InputBlocker> Blockers { get; set; } = new List<InputBlocker>();
        public IList<FormOption> Options { get; set; } = new List<FormOption>();
    }
}