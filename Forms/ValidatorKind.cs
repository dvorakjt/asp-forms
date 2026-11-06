namespace FormsSandbox.Forms
{
    public enum ValidatorKind
    {
        Required,
        MinLength,
        MaxLength,
        Pattern,
        OneOf,
        MatchesField,
        MinValue,
        MaxValue,
        Step,
        IsNumber,
        Custom
    }
}