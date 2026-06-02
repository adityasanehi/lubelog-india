namespace CarCareTracker.Models
{
    public class PushSubscriptionRecord
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string P256DH { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;
    }
}
