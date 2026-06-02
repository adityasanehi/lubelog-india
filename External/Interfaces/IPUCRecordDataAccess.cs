using CarCareTracker.Models;

namespace CarCareTracker.External.Interfaces
{
    public interface IPUCRecordDataAccess
    {
        List<PUCRecord> GetPUCRecordsByVehicleId(int vehicleId);
        PUCRecord GetPUCRecordById(int pucRecordId);
        bool DeletePUCRecordById(int pucRecordId);
        bool SavePUCRecordToVehicle(PUCRecord pucRecord);
        bool DeleteAllPUCRecordsByVehicleId(int vehicleId);
    }
}
