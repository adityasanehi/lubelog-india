using CarCareTracker.Models;

namespace CarCareTracker.External.Interfaces
{
    public interface IInsuranceRecordDataAccess
    {
        List<InsuranceRecord> GetInsuranceRecordsByVehicleId(int vehicleId);
        InsuranceRecord GetInsuranceRecordById(int insuranceRecordId);
        bool DeleteInsuranceRecordById(int insuranceRecordId);
        bool SaveInsuranceRecordToVehicle(InsuranceRecord insuranceRecord);
        bool DeleteAllInsuranceRecordsByVehicleId(int vehicleId);
    }
}
