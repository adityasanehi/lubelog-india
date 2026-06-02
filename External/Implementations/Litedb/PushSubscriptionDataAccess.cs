using CarCareTracker.External.Interfaces;
using CarCareTracker.Helper;
using CarCareTracker.Models;
using LiteDB;

namespace CarCareTracker.External.Implementations
{
    public class PushSubscriptionDataAccess : IPushSubscriptionDataAccess
    {
        private ILiteDBHelper _liteDB { get; set; }
        private static string tableName = "pushsubscriptions";
        public PushSubscriptionDataAccess(ILiteDBHelper liteDB)
        {
            _liteDB = liteDB;
        }
        public List<PushSubscriptionRecord> GetSubscriptionsByUserId(int userId)
        {
            var db = _liteDB.GetLiteDB();
            var table = db.GetCollection<PushSubscriptionRecord>(tableName);
            return table.Find(Query.EQ(nameof(PushSubscriptionRecord.UserId), userId)).ToList();
        }
        public bool SaveSubscription(PushSubscriptionRecord subscription)
        {
            var db = _liteDB.GetLiteDB();
            var table = db.GetCollection<PushSubscriptionRecord>(tableName);
            // Upsert by endpoint to avoid duplicates
            var existing = table.FindOne(x => x.Endpoint == subscription.Endpoint);
            if (existing != null)
            {
                subscription.Id = existing.Id;
            }
            table.Upsert(subscription);
            db.Checkpoint();
            return true;
        }
        public bool DeleteSubscriptionByEndpoint(string endpoint)
        {
            var db = _liteDB.GetLiteDB();
            var table = db.GetCollection<PushSubscriptionRecord>(tableName);
            var deleted = table.DeleteMany(x => x.Endpoint == endpoint);
            db.Checkpoint();
            return deleted > 0;
        }
        public bool DeleteAllSubscriptionsByUserId(int userId)
        {
            var db = _liteDB.GetLiteDB();
            var table = db.GetCollection<PushSubscriptionRecord>(tableName);
            table.DeleteMany(Query.EQ(nameof(PushSubscriptionRecord.UserId), userId));
            db.Checkpoint();
            return true;
        }
    }
}
