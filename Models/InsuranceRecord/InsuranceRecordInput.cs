namespace CarCareTracker.Models
{
    public class InsuranceRecordInput
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public List<int> ReminderRecordId { get; set; } = new List<int>();
        public string PolicyNumber { get; set; } = string.Empty;
        public string InsurerName { get; set; } = string.Empty;
        public string PolicyType { get; set; } = nameof(InsurancePolicyType.Comprehensive);
        public decimal Premium { get; set; }
        public string StartDate { get; set; } = DateTime.Now.ToShortDateString();
        public string ExpiryDate { get; set; } = DateTime.Now.AddYears(1).ToShortDateString();
        public string Notes { get; set; } = string.Empty;
        public List<UploadedFiles> Files { get; set; } = new List<UploadedFiles>();
        public List<string> Tags { get; set; } = new List<string>();
        public InsuranceRecord ToInsuranceRecord() => new InsuranceRecord
        {
            Id = Id,
            VehicleId = VehicleId,
            PolicyNumber = PolicyNumber,
            InsurerName = InsurerName,
            PolicyType = Enum.TryParse<InsurancePolicyType>(PolicyType, out var pt) ? pt : InsurancePolicyType.Comprehensive,
            Premium = Premium,
            StartDate = DateTime.Parse(StartDate),
            ExpiryDate = DateTime.Parse(ExpiryDate),
            Notes = Notes,
            Files = Files,
            Tags = Tags
        };
    }
}
