using CarCareTracker.External.Interfaces;
using CarCareTracker.Helper;
using CarCareTracker.Models;
using LiteDB;

namespace CarCareTracker.External.Implementations
{
    public class PUCRecordDataAccess : IPUCRecordDataAccess
    {
        private ILiteDBHelper _liteDB { get; set; }
        private static string tableName = "pucrecords";
        public PUCRecordDataAccess(ILiteDBHelper liteDB)
        {
            _liteDB = liteDB;
        }
        public List<PUCRecord> GetPUCRecordsByVehicleId(int vehicleId)
        {
            var db = _liteDB.GetLiteDB();
            var table = db.GetCollection<PUCRecord>(tableName);
            return table.Find(Query.EQ(nameof(PUCRecord.VehicleId), vehicleId)).ToList();
        }
        public PUCRecord GetPUCRecordById(int pucRecordId)
        {
            var db = _liteDB.GetLiteDB();
            var table = db.GetCollection<PUCRecord>(tableName);
            return table.FindById(pucRecordId);
        }
        public bool DeletePUCRecordById(int pucRecordId)
        {
            var db = _liteDB.GetLiteDB();
            var table = db.GetCollection<PUCRecord>(tableName);
            table.Delete(pucRecordId);
            db.Checkpoint();
            return true;
        }
        public bool SavePUCRecordToVehicle(PUCRecord pucRecord)
        {
            var db = _liteDB.GetLiteDB();
            var table = db.GetCollection<PUCRecord>(tableName);
            table.Upsert(pucRecord);
            db.Checkpoint();
            return true;
        }
        public bool DeleteAllPUCRecordsByVehicleId(int vehicleId)
        {
            var db = _liteDB.GetLiteDB();
            var table = db.GetCollection<PUCRecord>(tableName);
            table.DeleteMany(Query.EQ(nameof(PUCRecord.VehicleId), vehicleId));
            db.Checkpoint();
            return true;
        }
    }
}
