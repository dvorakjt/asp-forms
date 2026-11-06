using System;
using System.Collections;
using System.Web;

namespace FormsSandbox.Examples.Tests
{
    internal sealed class TestHttpContext : HttpContextBase
    {
        private readonly IDictionary items = new Hashtable();

        public override IDictionary Items => items;
    }
}