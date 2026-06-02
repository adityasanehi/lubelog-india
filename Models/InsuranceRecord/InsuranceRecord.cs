namespace CarCareTracker.Models
{
    public enum InsurancePolicyType
    {
        Comprehensive,
        ThirdParty,
        OwnDamage
    }

    public class InsuranceRecord
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public string PolicyNumber { get; set; } = string.Empty;
        public string InsurerName { get; set; } = string.Empty;
        public InsurancePolicyType PolicyType { get; set; } = InsurancePolicyType.Comprehensive;
        public decimal Premium { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Notes { get; set; } = string.Empty;
        public List<UploadedFiles> Files { get; set; } = new List<UploadedFiles>();
        public List<string> Tags { get; set; } = new List<string>();
    }
}
