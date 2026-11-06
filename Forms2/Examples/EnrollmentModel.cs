namespace FormsSandbox.Forms2.Examples
{
    public sealed class EnrollmentModel
    {
        public string Plan { get; set; }
        public string CardChoice { get; set; }
        public string SavedCard { get; set; }
        public int? ExpiryMonth { get; set; }
        public int? ExpiryYear { get; set; }
        public decimal? Amount { get; set; }
    }
}