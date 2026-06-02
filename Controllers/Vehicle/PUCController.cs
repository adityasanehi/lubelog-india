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
        public IActionResult GetPUCRecordsByVehicleId(int vehicleId)
        {
            var result = _pucRecordDataAccess.GetPUCRecordsByVehicleId(vehicleId);
            bool _useDescending = _config.GetUserConfig(User).UseDescending;
            if (_useDescending)
                result = result.OrderByDescending(x => x.ExpiryDate).ToList();
            else
                result = result.OrderBy(x => x.ExpiryDate).ToList();
            return PartialView("PUC/_PUCRecords", result);
        }

        [HttpPost]
        public IActionResult SavePUCRecordToVehicleId(PUCRecordInput pucRecord)
        {
            if (!_userLogic.UserCanEditVehicle(GetUserID(), pucRecord.VehicleId, HouseholdPermission.Edit))
                return Json(OperationResponse.Failed("Access Denied"));
            pucRecord.Files = pucRecord.Files.Select(x => new UploadedFiles { Name = x.Name, Location = _fileHelper.MoveFileFromTemp(x.Location, "documents/") }).ToList();
            if (pucRecord.ReminderRecordId.Any())
            {
                foreach (int reminderRecordId in pucRecord.ReminderRecordId)
                    PushbackRecurringReminderRecordWithChecks(reminderRecordId, DateTime.Parse(pucRecord.ExpiryDate), null);
            }
            var record = pucRecord.ToPUCRecord();
            var result = _pucRecordDataAccess.SavePUCRecordToVehicle(record);
            return Json(OperationResponse.Conditional(result, string.Empty, StaticHelper.GenericErrorMessage));
        }

        [HttpGet]
        public IActionResult GetAddPUCRecordPartialView()
        {
            return PartialView("PUC/_PUCRecordModal", new PUCRecordInput());
        }

        [HttpGet]
        public IActionResult GetPUCRecordForEditById(int pucRecordId)
        {
            var result = _pucRecordDataAccess.GetPUCRecordById(pucRecordId);
            if (!_userLogic.UserCanEditVehicle(GetUserID(), result.VehicleId, HouseholdPermission.View))
                return Redirect("/Error/Unauthorized");
            var convertedResult = new PUCRecordInput
            {
                Id = result.Id,
                VehicleId = result.VehicleId,
                CertificateNumber = result.CertificateNumber,
                TestCenter = result.TestCenter,
                TestDate = result.TestDate.ToShortDateString(),
                ExpiryDate = result.ExpiryDate.ToShortDateString(),
                Notes = result.Notes,
                Files = result.Files,
                Tags = result.Tags
            };
            return PartialView("PUC/_PUCRecordModal", convertedResult);
        }

        private OperationResponse DeletePUCRecordWithChecks(int pucRecordId)
        {
            var existingRecord = _pucRecordDataAccess.GetPUCRecordById(pucRecordId);
            if (!_userLogic.UserCanEditVehicle(GetUserID(), existingRecord.VehicleId, HouseholdPermission.Delete))
                return OperationResponse.Failed("Access Denied");
            var result = _pucRecordDataAccess.DeletePUCRecordById(existingRecord.Id);
            return OperationResponse.Conditional(result, string.Empty, StaticHelper.GenericErrorMessage);
        }

        [HttpPost]
        public IActionResult DeletePUCRecordById(int pucRecordId)
        {
            return Json(DeletePUCRecordWithChecks(pucRecordId));
        }
    }
}
