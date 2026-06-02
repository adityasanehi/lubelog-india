namespace CarCareTracker.Models
{
    public class PUCRecord
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public string CertificateNumber { get; set; } = string.Empty;
        public string TestCenter { get; set; } = string.Empty;
        public DateTime TestDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Notes { get; set; } = string.Empty;
        public List<UploadedFiles> Files { get; set; } = new List<UploadedFiles>();
        public List<string> Tags { get; set; } = new List<string>();
    }
}
