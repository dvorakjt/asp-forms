namespace FormsSandbox.Models
{
    // Raw MVC-bound strings; validated and converted values come from the form definition.
    public class DocumentForm
    {
        public string Title { get; set; }
        public string Summary { get; set; }
        public string Body { get; set; }
        public string Category { get; set; }
        public string Amount { get; set; }
        public string AcceptTerms { get; set; }
        public string[] Topics { get; set; }
        public string Phone { get; set; }
    }
}