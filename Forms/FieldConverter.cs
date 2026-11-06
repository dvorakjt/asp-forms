namespace FormsSandbox.Forms
{
    public sealed class FieldConverter
    {
        public FieldValueType Type { get; set; } = FieldValueType.String;
        public string ErrorMessage { get; set; } = "Enter a valid value.";
    }
}