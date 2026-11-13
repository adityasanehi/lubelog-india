using CarCareTracker.Filter;
using CarCareTracker.Helper;
using CarCareTracker.Models;
using Microsoft.AspNetCore.Mvc;

namespace CarCareTracker.Controllers;

public partial class APIController
{
    private bool IsDryRun => bool.TryParse(Request.Query["dryRun"], out var value) && value;
    private static string WorkshopTable(string type) => type.ToLowerInvariant() switch
    {
        "service" => "servicerecords", "repair" => "collisionrecords", "upgrade" => "upgraderecords",
        _ => throw new ArgumentException("recordType must be service, repair or upgrade")
    };

    private void RequireWorkshopAccess(int vehicleId)
    {
        if (vehicleId <= 0 || _dataAccess.GetVehicleById(vehicleId)?.Id != vehicleId)
            throw new ArgumentException("Invalid vehicle id");
        if (!_userLogic.UserCanEditVehicle(GetUserID(), vehicleId, HouseholdPermission.Edit))
            throw new UnauthorizedAccessException("Access denied");
    }

    private IActionResult SaveWorkshopRecord(string type, int vehicleId, GenericRecordExportModel input, bool update)
    {
        try
        {
            var table = WorkshopTable(type);
            if (!DateTime.TryParse(input.Date, out var date) || !int.TryParse(input.Odometer, out var mileage) || mileage < 0 ||
                !decimal.TryParse(input.Cost, out var cost) || cost < 0 || string.IsNullOrWhiteSpace(input.Description))
                throw new ArgumentException("Valid date, nonnegative odometer/cost and description are required");
            var id = 0;
            if (update && (!int.TryParse(input.Id, out id) || id <= 0)) throw new ArgumentException("Invalid record id");
            using var tx = new WorkshopTransaction(_configuration, _liteDB);
            var record = update ? tx.Get<GenericRecord>(table, id) ?? throw new ArgumentException("Invalid record id") : new GenericRecord { VehicleId = vehicleId };
            RequireWorkshopAccess(record.VehicleId);
            var requested = input.Requisitions ?? new();
            if (requested.Any(x => x == null || x.SupplyRecordId <= 0 || x.Quantity <= 0))
                throw new ArgumentException("Each requisition needs a supplyRecordId and positive quantity");
            var supplies = new List<(SupplyRecord supply, decimal quantity)>();
            foreach (var group in requested.GroupBy(x => x.SupplyRecordId).OrderBy(x => x.Key))
            {
                var quantity = group.Sum(x => x.Quantity);
                var supply = tx.Get<SupplyRecord>("supplyrecords", group.Key) ?? throw new ArgumentException("Supply not found");
                if (supply.VehicleId != record.VehicleId && supply.VehicleId != 0)
                    throw new UnauthorizedAccessException("Supply belongs to a different vehicle");
                if (supply.VehicleId == 0 && !_config.GetServerEnableShopSupplies())
                    throw new UnauthorizedAccessException("Shop supplies are disabled");
                if (quantity > supply.Quantity) throw new ArgumentException($"Insufficient stock for supply {group.Key}");
                supplies.Add((supply, quantity));
            }
            record.Date = date; record.Mileage = mileage; record.Cost = cost;
            record.Description = input.Description; record.Notes = input.Notes ?? "";
            record.Files = input.Files ?? new(); record.ExtraFields = input.ExtraFields ?? new();
            record.Tags = (input.Tags ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
            if (IsDryRun) return Json(OperationResponse.Succeed("Validated; no writes", new { dryRun = true, recordId = update ? id : (int?)null, requisitions = requested, cost }));
            foreach (var (supply, quantity) in supplies)
            {
                record.RequisitionHistory.Add(SupplyRequisition.Apply(supply, quantity, date, record.Description));
                tx.Save("supplyrecords", supply, supply.Id, supply.VehicleId);
            }
            record.Id = tx.Save(table, record, id, record.VehicleId);
            // Use the same auto-odometer behavior, within the stock/record transaction.
            if (!update && mileage != 0 && _config.GetUserConfig(User).EnableAutoOdometerInsert)
            {
                var previous = _odometerRecordDataAccess.GetOdometerRecordsByVehicleId(record.VehicleId).Select(x => x.Mileage).DefaultIfEmpty(0).Max();
                var odometer = new OdometerRecord { VehicleId = record.VehicleId, Date = date, Mileage = mileage, InitialMileage = previous == 0 ? mileage : previous, Notes = record.Notes,
                    EquipmentRecordId = _equipmentRecordDataAccess.GetEquipmentRecordsByVehicleId(record.VehicleId).Where(x => x.IsEquipped).Select(x => x.Id).ToList(),
                    Files = StaticHelper.CreateAttachmentFromRecord(type switch { "service" => ImportMode.ServiceRecord, "repair" => ImportMode.RepairRecord, _ => ImportMode.UpgradeRecord }, record.Id, record.Description) };
                tx.Save("odometerrecords", odometer, 0, record.VehicleId);
            }
            tx.Commit();
            try
            {
                _eventLogic.PublishEvent(GetUserID(), WebHookPayload.FromGenericRecord(record, $"{type}record.{(update ? "update" : "add")}.api", User.Identity?.Name ?? string.Empty));
            }
            catch (Exception ex)
            {
                // A notification failure must not invite replay of committed stock consumption.
                HttpContext.RequestServices.GetRequiredService<ILogger<APIController>>().LogWarning(ex, "Workshop record {RecordId} committed; event publication failed", record.Id);
            }
            return Json(OperationResponse.Succeed("Workshop record saved", new { recordId = record.Id }));
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, OperationResponse.Failed(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(OperationResponse.Failed(ex.Message)); }
        catch (OverflowException) { return BadRequest(OperationResponse.Failed("Quantity or cost out of range")); }
    }

    [TypeFilter(typeof(APIKeyFilter), Arguments = new object[] { HouseholdPermission.Edit })]
    [HttpPost("/api/workshop/{recordType}/{recordId:int}/documents")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> AttachWorkshopDocuments(string recordType, int recordId, List<IFormFile> documents)
    {
        var created = new List<string>();
        try
        {
            var table = WorkshopTable(recordType);
            if (recordId <= 0 || documents == null || documents.Count == 0 || documents.Sum(x => x.Length) > DocumentValidation.MaxBytes)
                throw new ArgumentException("Provide documents totaling at most 10 MiB");
            var contents = new List<(string name, byte[] bytes, string extension)>();
            foreach (var file in documents)
            {
                if (file.Length <= 0 || file.Length > DocumentValidation.MaxBytes) throw new ArgumentException("Invalid document size");
                using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer);
                var bytes = buffer.ToArray();
                var extension = DocumentValidation.Validate(file.FileName, file.ContentType, bytes);
                contents.Add((Path.GetFileName(file.FileName.Replace('\\', '/')), bytes, extension));
            }
            // LiteDB transactions are thread-bound: all awaits precede BeginTrans.
            using var tx = new WorkshopTransaction(_configuration, _liteDB);
            var record = tx.Get<GenericRecord>(table, recordId) ?? throw new ArgumentException("Record not found");
            RequireWorkshopAccess(record.VehicleId);
            if (IsDryRun) return Json(OperationResponse.Succeed("Validated; no writes", new { dryRun = true, recordId }));
            var directory = Path.Combine(_webEnv.ContentRootPath, "data", "documents");
            Directory.CreateDirectory(directory);
            foreach (var (name, bytes, extension) in contents)
            {
                var fileName = Guid.NewGuid().ToString() + extension;
                var path = Path.Combine(directory, fileName);
                created.Add(path);
                System.IO.File.WriteAllBytes(path, bytes);
                record.Files.Add(new UploadedFiles { Name = name, Location = "/documents/" + fileName });
            }
            tx.Save(table, record, record.Id, record.VehicleId);
            tx.Commit();
            created.Clear();
            return Json(OperationResponse.Succeed("Documents attached", new { recordId, files = record.Files }));
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, OperationResponse.Failed(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(OperationResponse.Failed(ex.Message)); }
        finally { foreach (var path in created) System.IO.File.Delete(path); }
    }
}
