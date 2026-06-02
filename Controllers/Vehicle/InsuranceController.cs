using CarCareTracker.Filter;
using CarCareTracker.Helper;
using CarCareTracker.Models;
using Microsoft.AspNetCore.Mvc;

namespace CarCareTracker.Controllers
{
    public partial class VehicleController
    {
        [TypeFilter(typeof(CollaboratorFilter))]
        [HttpGet]
        public IActionResult GetInsuranceRecordsByVehicleId(int vehicleId)
        {
            var result = _insuranceRecordDataAccess.GetInsuranceRecordsByVehicleId(vehicleId);
            bool _useDescending = _config.GetUserConfig(User).UseDescending;
            if (_useDescending)
                result = result.OrderByDescending(x => x.ExpiryDate).ToList();
            else
                result = result.OrderBy(x => x.ExpiryDate).ToList();
            return PartialView("Insurance/_InsuranceRecords", result);
        }

        [HttpPost]
        public IActionResult SaveInsuranceRecordToVehicleId(InsuranceRecordInput insuranceRecord)
        {
            if (!_userLogic.UserCanEditVehicle(GetUserID(), insuranceRecord.VehicleId, HouseholdPermission.Edit))
                return Json(OperationResponse.Failed("Access Denied"));
            insuranceRecord.Files = insuranceRecord.Files.Select(x => new UploadedFiles { Name = x.Name, Location = _fileHelper.MoveFileFromTemp(x.Location, "documents/") }).ToList();
            if (insuranceRecord.ReminderRecordId.Any())
            {
                foreach (int reminderRecordId in insuranceRecord.ReminderRecordId)
                    PushbackRecurringReminderRecordWithChecks(reminderRecordId, DateTime.Parse(insuranceRecord.ExpiryDate), null);
            }
            var record = insuranceRecord.ToInsuranceRecord();
            var result = _insuranceRecordDataAccess.SaveInsuranceRecordToVehicle(record);
            return Json(OperationResponse.Conditional(result, string.Empty, StaticHelper.GenericErrorMessage));
        }

        [HttpGet]
        public IActionResult GetAddInsuranceRecordPartialView()
        {
            return PartialView("Insurance/_InsuranceRecordModal", new InsuranceRecordInput());
        }

        [HttpGet]
        public IActionResult GetInsuranceRecordForEditById(int insuranceRecordId)
        {
            var result = _insuranceRecordDataAccess.GetInsuranceRecordById(insuranceRecordId);
            if (!_userLogic.UserCanEditVehicle(GetUserID(), result.VehicleId, HouseholdPermission.View))
                return Redirect("/Error/Unauthorized");
            var convertedResult = new InsuranceRecordInput
            {
                Id = result.Id,
                VehicleId = result.VehicleId,
                PolicyNumber = result.PolicyNumber,
                InsurerName = result.InsurerName,
                PolicyType = result.PolicyType.ToString(),
                Premium = result.Premium,
                StartDate = result.StartDate.ToShortDateString(),
                ExpiryDate = result.ExpiryDate.ToShortDateString(),
                Notes = result.Notes,
                Files = result.Files,
                Tags = result.Tags
            };
            return PartialView("Insurance/_InsuranceRecordModal", convertedResult);
        }

        private OperationResponse DeleteInsuranceRecordWithChecks(int insuranceRecordId)
        {
            var existingRecord = _insuranceRecordDataAccess.GetInsuranceRecordById(insuranceRecordId);
            if (!_userLogic.UserCanEditVehicle(GetUserID(), existingRecord.VehicleId, HouseholdPermission.Delete))
                return OperationResponse.Failed("Access Denied");
            var result = _insuranceRecordDataAccess.DeleteInsuranceRecordById(existingRecord.Id);
            return OperationResponse.Conditional(result, string.Empty, StaticHelper.GenericErrorMessage);
        }

        [HttpPost]
        public IActionResult DeleteInsuranceRecordById(int insuranceRecordId)
        {
            return Json(DeleteInsuranceRecordWithChecks(insuranceRecordId));
        }
    }
}
