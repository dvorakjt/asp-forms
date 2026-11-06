namespace FormsSandbox.Forms2
{
    public interface IFieldState
    {
        string Name { get; }
        string Raw { get; }
        bool IsPresent { get; }
        bool ConversionSucceeded { get; }
        bool IsIncluded { get; }
        bool IsValid { get; }
        string Error { get; }
        object BoxedValue { get; }
        void SetIncluded(bool included);
    }

    public sealed class FieldState<T> : IFieldState
    {
        public string Name { get; internal set; }
        public string Raw { get; internal set; }
        public bool IsPresent { get; internal set; }
        public bool ConversionSucceeded { get; internal set; }
        public bool IsIncluded { get; internal set; }
        public bool IsValid => IsIncluded && ConversionSucceeded && Error == null;
        public string Error { get; internal set; }
        public T Value { get; internal set; }
        public object BoxedValue => Value;
        void IFieldState.SetIncluded(bool included) { IsIncluded = included; }
    }
}