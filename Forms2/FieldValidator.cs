using System;
using System.Threading.Tasks;

namespace FormsSandbox.Forms2
{
    public sealed class FieldValidator<T>
    {
        public int Order { get; set; }
        public string Message { get; set; }
        public ActionScope Scope { get; set; } = new ActionScope();
        // Cross-field rules run after all fields have passed their local checks.
        public bool CrossField { get; set; }
        public Func<FieldState<T>, FormSnapshot, bool> Check { get; set; }
        // Async checks run only on the server, during the final POST.
        public Func<FieldState<T>, FormSnapshot, Task<bool>> CheckOnServerAsync { get; set; }
    }
}