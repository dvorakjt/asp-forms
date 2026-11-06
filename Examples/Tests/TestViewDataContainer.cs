using System.Web.Mvc;

namespace FormsSandbox.Examples.Tests
{
    internal sealed class TestViewDataContainer : IViewDataContainer
    {
        public ViewDataDictionary ViewData { get; set; } = new ViewDataDictionary();
    }
}