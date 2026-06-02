using CarCareTracker.Models;

namespace CarCareTracker.External.Interfaces
{
    public interface IPushSubscriptionDataAccess
    {
        List<PushSubscriptionRecord> GetSubscriptionsByUserId(int userId);
        bool SaveSubscription(PushSubscriptionRecord subscription);
        bool DeleteSubscriptionByEndpoint(string endpoint);
        bool DeleteAllSubscriptionsByUserId(int userId);
    }
}
