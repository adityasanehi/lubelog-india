namespace CarCareTracker.Models
{
    public class PUCRecordInput
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public List<int> ReminderRecordId { get; set; } = new List<int>();
        public string CertificateNumber { get; set; } = string.Empty;
        public string TestCenter { get; set; } = string.Empty;
        public string TestDate { get; set; } = DateTime.Now.ToShortDateString();
        public string ExpiryDate { get; set; } = DateTime.Now.AddMonths(6).ToShortDateString();
        public string Notes { get; set; } = string.Empty;
        public List<UploadedFiles> Files { get; set; } = new List<UploadedFiles>();
        public List<string> Tags { get; set; } = new List<string>();
        public PUCRecord ToPUCRecord() => new PUCRecord
        {
            Id = Id,
            VehicleId = VehicleId,
            CertificateNumber = CertificateNumber,
            TestCenter = TestCenter,
            TestDate = DateTime.Parse(TestDate),
            ExpiryDate = DateTime.Parse(ExpiryDate),
            Notes = Notes,
            Files = Files,
            Tags = Tags
        };
    }
}
