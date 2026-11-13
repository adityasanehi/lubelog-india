using CarCareTracker.Helper;
using CarCareTracker.Models;
using LiteDB;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace WorkshopChecks;

public static class Checks
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static async Task Main(string[] args)
    {
        if (args.Contains("seed")) { Seed(); return; }
        if (args.Contains("unit")) { Unit(); return; }
        using var api = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:18080") };
        api.DefaultRequestHeaders.Add("x-api-key", "workshop-test-only");
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try { using var ready = await api.GetAsync("/api/version"); if (ready.IsSuccessStatusCode) break; }
            catch (HttpRequestException) { }
            await Task.Delay(100);
        }
        var vehicle = (await Post(api, "/api/vehicles/add", new { year = 2026, make = "Workshop", model = "Test", identifier = "LicensePlate", licensePlate = "TEST", fuelType = "Gasoline" }))["additionalData"]!["vehicleId"]!.GetValue<int>();
        async Task<int> Supply(int v, decimal quantity = 2) => (await Post(api, $"/api/vehicle/supplyrecords/add?vehicleId={v}", new { date = "2026-11-13", description = "Test part", partQuantity = quantity, cost = 200 }))["additionalData"]!["recordId"]!.GetValue<int>();
        using (var denied = new HttpClient { BaseAddress = api.BaseAddress })
        {
            using var unauthenticated = await denied.GetAsync("/api/vehicles");
            Assert(!unauthenticated.IsSuccessStatusCode, "Missing auth was accepted");
            denied.DefaultRequestHeaders.Add("x-api-key", "workshop-view-only");
            using var forbidden = await denied.PostAsJsonAsync($"/api/vehicle/supplyrecords/add?vehicleId={vehicle}&dryRun=true", new { date = "2026-11-13", description = "Forbidden", partQuantity = 1, cost = 1 });
            Assert(!forbidden.IsSuccessStatusCode, "dryRun bypassed API key edit permission");
        }
        var supply = await Supply(vehicle);
        var shop = await Supply(0);
        object Data(object[] requisitions) => new { date = "2026-11-13", odometer = 120, description = "Workshop test", cost = 500, requisitions };
        async Task<decimal> Stock(int id) => JsonNode.Parse(await api.GetStringAsync($"/api/vehicle/supplyrecords?vehicleId={vehicle}&id={id}"))![0]!["partQuantity"]!.ToString() is var q ? decimal.Parse(q) : -1;
        foreach (var invalid in new[] { 0m, -1m, 3m })
        {
            using var response = await api.PostAsJsonAsync($"/api/vehicle/servicerecords/add?vehicleId={vehicle}", Data([new { supplyRecordId = supply, quantity = invalid }]));
            Assert(!response.IsSuccessStatusCode, "Invalid requisition accepted");
            Assert(await Stock(supply) == 2, "Failed request changed stock");
        }
        using (var response = await api.PostAsJsonAsync($"/api/vehicle/servicerecords/add?vehicleId={vehicle}", Data([new { supplyRecordId = supply, quantity = 1 }, new { supplyRecordId = int.MaxValue, quantity = 1 }])))
            Assert(!response.IsSuccessStatusCode, "Mixed invalid requisition accepted");
        Assert(await Stock(supply) == 2, "Mixed request partially consumed stock");
        using (var response = await api.PostAsJsonAsync($"/api/vehicle/servicerecords/add?vehicleId={vehicle}", Data([new { supplyRecordId = supply, quantity = 1.5m }, new { supplyRecordId = supply, quantity = 1m }])))
            Assert(!response.IsSuccessStatusCode, "Duplicate quantities bypassed limit");
        await Post(api, $"/api/vehicle/servicerecords/add?vehicleId={vehicle}&dryRun=true", Data([new { supplyRecordId = supply, quantity = 1 }]));
        Assert(await Stock(supply) == 2, "Dry run changed stock");
        var added = await Post(api, $"/api/vehicle/servicerecords/add?vehicleId={vehicle}", Data([new { supplyRecordId = supply, quantity = 1 }, new { supplyRecordId = shop, quantity = .5m }]));
        var recordId = added["additionalData"]!["recordId"]!.GetValue<int>();
        Assert(await Stock(supply) == 1, "Stock did not decrement");
        var records = JsonNode.Parse(await api.GetStringAsync($"/api/vehicle/servicerecords?vehicleId={vehicle}&id={recordId}"))!;
        Assert(records[0]!["cost"]!.ToString() == "500", "Total cost changed");
        Assert(records[0]!["requisitionHistory"]!.AsArray().Count == 2, "Missing requisition history");
        foreach (var type in new[] { "service", "repair", "upgrade" })
        {
            var stockId = await Supply(vehicle);
            var job = await Post(api, $"/api/vehicle/{type}records/add?vehicleId={vehicle}", Data([new { supplyRecordId = stockId, quantity = .5m }]));
            var jobId = job["additionalData"]!["recordId"]!.GetValue<int>();
            using var changed = await api.PutAsJsonAsync($"/api/vehicle/{type}records/update", new { id = jobId, date = "2026-11-13", odometer = 125, description = "Updated job", cost = 600, requisitions = new[] { new { supplyRecordId = stockId, quantity = .5m } } });
            Assert(changed.IsSuccessStatusCode, "Update with requisitions failed");
            Assert(await Stock(stockId) == 1, "Update did not append consumption");
        }
        var raceSupply = await Supply(vehicle, 1);
        var race = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => api.PostAsJsonAsync($"/api/vehicle/servicerecords/add?vehicleId={vehicle}", Data([new { supplyRecordId = raceSupply, quantity = 1 }]))));
        Assert(race.Count(x => x.IsSuccessStatusCode) == 1 && await Stock(raceSupply) == 0, "Concurrent requisition oversold stock");
        foreach (var response in race) response.Dispose();
        await McpChecks(vehicle, supply, recordId);
        Console.WriteLine("PASS: requisition limits, duplicate totals, mixed-request rollback, dry run, vehicle/shop stock, total costs, histories, edits/preservation, auth passthrough and MCP end-to-end");
    }

    static async Task<JsonNode> Post(HttpClient api, string path, object data)
    {
        using var response = await api.PostAsJsonAsync(path, data);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert(response.IsSuccessStatusCode && body["success"]?.GetValue<bool>() == true, "API write failed: " + body);
        return body;
    }

    static void Unit()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(dir);
        var previous = Directory.GetCurrentDirectory(); Directory.SetCurrentDirectory(dir); Directory.CreateDirectory("data");
        var helper = new LiteDBHelper();
        try
        {
            var supply = new SupplyRecord { VehicleId = 1, Quantity = 2, Cost = 20, Description = "part" };
            var id = helper.GetLiteDB().GetCollection<SupplyRecord>("supplyrecords").Insert(supply).AsInt32;
            using (var tx = new WorkshopTransaction(new ConfigurationBuilder().Build(), helper))
            {
                supply = tx.Get<SupplyRecord>("supplyrecords", id)!;
                SupplyRequisition.Apply(supply, 1, DateTime.Today, "job"); tx.Save("supplyrecords", supply, id, 1);
                tx.Save("servicerecords", new GenericRecord { VehicleId = 1 }, 0, 1);
                // Dispose without committing simulates a storage failure after the first writes.
            }
            Assert(helper.GetLiteDB().GetCollection<SupplyRecord>("supplyrecords").FindById(id).Quantity == 2, "Transaction did not roll back stock");
            Assert(helper.GetLiteDB().GetCollection<GenericRecord>("servicerecords").Count() == 0, "Transaction did not roll back record");
            Assert(DocumentValidation.Validate("a.pdf", "application/pdf", "%PDF-1.7"u8.ToArray()) == ".pdf", "PDF rejected");
            foreach (var (name, mime, bytes) in new[] { ("a.exe", "application/pdf", "%PDF-1.7"u8.ToArray()), ("a.pdf", "image/png", "%PDF-1.7"u8.ToArray()), ("a.pdf", "application/pdf", "junk"u8.ToArray()), ("a.pdf", "application/pdf", new byte[DocumentValidation.MaxBytes + 1]) })
            { var rejected = false; try { DocumentValidation.Validate(name, mime, bytes); } catch (ArgumentException) { rejected = true; } Assert(rejected, "Invalid attachment accepted"); }
            Console.WriteLine("PASS: storage rollback after writes and attachment validation");
        }
        finally { helper.DisposeLiteDB(); Directory.SetCurrentDirectory(previous); Directory.Delete(dir, true); }
    }

    static void Seed()
    {
        Directory.CreateDirectory("data");
        using var db = new LiteDatabase(StaticHelper.DbName);
        // Only run against the disposable copy. Disable all inherited authentication material.
        foreach (var table in new[] { "apikeyrecords", "userrecords", "userconfigs", "tokenrecords" }) db.DropCollection(table);
        db.GetCollection<APIKey>("apikeyrecords").Insert(new APIKey { UserId = -1, Name = "Workshop test", Key = StaticHelper.GetHash("workshop-test-only"), Permissions = [HouseholdPermission.View, HouseholdPermission.Edit] });
        db.GetCollection<APIKey>("apikeyrecords").Insert(new APIKey { UserId = -1, Name = "Read-only fixture", Key = StaticHelper.GetHash("workshop-view-only"), Permissions = [HouseholdPermission.View] });
        foreach (var type in new[] { ImportMode.ServiceRecord, ImportMode.RepairRecord, ImportMode.UpgradeRecord })
            db.GetCollection<RecordExtraField>("extrafields").Upsert(new RecordExtraField { Id = (int)type, ExtraFields = [new ExtraField { Name = "Labour Cost", FieldType = ExtraFieldType.Decimal }, new ExtraField { Name = "Parts Cost", FieldType = ExtraFieldType.Decimal }] });
        Directory.CreateDirectory("data/config");
        File.WriteAllText(StaticHelper.UserConfigPath, System.Text.Json.JsonSerializer.Serialize(new { EnableAuth = true, EnableShopSupplies = true, EnableAutoOdometerInsert = true, UserNameHash = StaticHelper.GetHash("workshop"), UserPasswordHash = StaticHelper.GetHash("workshop-test-only") }));
        File.WriteAllText(StaticHelper.ServerConfigPath, "{}");
        Console.WriteLine("Disposable fixture authentication seeded");
    }

    static async Task McpChecks(int vehicle, int supply, int existingId)
    {
        var endpoint = Environment.GetEnvironmentVariable("MCP_URL") ?? "http://localhost:18081/api/mcp";
        foreach (var mode in new[] { "x-api-key", "query", "Authorization" })
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");
            if (mode == "Authorization") client.DefaultRequestHeaders.Add(mode, "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("workshop:workshop-test-only")));
            else if (mode != "query") client.DefaultRequestHeaders.Add(mode, "workshop-test-only");
            var url = endpoint + (mode == "query" ? "?apiKey=workshop-test-only" : "");
            var seq = 0;
            async Task<JsonNode> Rpc(string method, object parameters)
            {
                using var response = await client.PostAsJsonAsync(url, new { jsonrpc = "2.0", id = ++seq, method, @params = parameters });
                if (response.Headers.TryGetValues("Mcp-Session-Id", out var sessions)) { client.DefaultRequestHeaders.Remove("Mcp-Session-Id"); client.DefaultRequestHeaders.Add("Mcp-Session-Id", sessions.First()); }
                var text = await response.Content.ReadAsStringAsync();
                Assert(response.IsSuccessStatusCode, "MCP HTTP failure: " + text);
                if (text.StartsWith("event:") || text.StartsWith("data:")) text = string.Join("\n", text.Split('\n').Where(x => x.StartsWith("data:")).Select(x => x[5..].Trim()));
                var node = JsonNode.Parse(text)!; Assert(node["error"] == null, "MCP protocol error: " + node); return node["result"]!;
            }
            await Rpc("initialize", new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "workshop-checks", version = "1" } });
            client.DefaultRequestHeaders.Add("MCP-Protocol-Version", "2025-06-18");
            var tools = (await Rpc("tools/list", new { }))["tools"]!.AsArray();
            string Tool(string name) => tools.First(x => x!["name"]!.GetValue<string>().Replace("_", "").Equals(name.Replace("_", ""), StringComparison.OrdinalIgnoreCase))!["name"]!.GetValue<string>();
            async Task<JsonNode> Call(string name, object arguments, bool success = true)
            {
                var result = await Rpc("tools/call", new { name = Tool(name), arguments });
                if (success) Assert(result["isError"]?.GetValue<bool>() != true, "MCP tool failed: " + result);
                return result;
            }
            Assert(!tools.Any(x => x!["name"]!.ToString().Contains("delete")), "Delete tools must not be exposed");
            foreach (var tool in tools.Where(x => x!["name"]!.ToString().StartsWith("get_")))
                Assert(tool!["annotations"]?["readOnlyHint"]?.GetValue<bool>() == true, "Read tool lacks read-only annotation");
            if (mode == "x-api-key") Console.WriteLine("MCP tools: " + string.Join(", ", tools.Select(x => x!["name"]!.ToString())));
            Assert(tools.Any(x => x!["name"]!.GetValue<string>() == "get_record_by_id" && x["annotations"]?["readOnlyHint"]?.GetValue<bool>() == true), "Read-only annotations missing");
            var read = await Call("get_record_by_id", new { recordType = "service", recordId = existingId });
            Assert(read.ToJsonString().Contains("Workshop test"), "Auth passthrough did not read fixture");
            if (mode != "x-api-key") continue;
            foreach (var tool in tools.Where(x => x!["name"]!.ToString().StartsWith("add_") || x["name"]!.ToString().StartsWith("update_") || x["name"]!.ToString() == "attach_document_to_record"))
                Assert(tool!["inputSchema"]?["properties"]?["dryRun"] != null, "Write tool lacks dryRun");
            var dryWrites = new (string name, object args, string path)[]
            {
                ("AddFuelRecord", new { vehicleId = vehicle, date = "2026-11-13", odometer = 123, volume = 1, cost = 100, fillToFull = true, missedFuelUp = false, startingSoc = 20, endingSoc = 80, extraFields = Array.Empty<object>(), dryRun = true }, $"/api/vehicle/gasrecords?vehicleId={vehicle}"),
                ("AddOdometerRecord", new { vehicleId = vehicle, date = "2026-11-13", odometer = 123, extraFields = Array.Empty<object>(), equipmentRecordIds = Array.Empty<int>(), dryRun = true }, $"/api/vehicle/odometerrecords?vehicleId={vehicle}"),
                ("AddVehicleRecord", new { year = 2026, make = "Dry", model = "Run", licensePlate = "DRY", useEngineHours = false, odometerOptional = false, fuelType = 0, extraFields = Array.Empty<object>(), dryRun = true }, "/api/vehicles"),
                ("AddSupplyRecord", new { vehicleId = vehicle, date = "2026-11-13", description = "Dry part", quantity = 1, cost = 100, partNumber = "DRY", partSupplier = "fixture", extraFields = Array.Empty<object>(), dryRun = true }, $"/api/vehicle/supplyrecords?vehicleId={vehicle}"),
                ("AddShopSupplyRecord", new { date = "2026-11-13", description = "Dry part", quantity = 1, cost = 100, partNumber = "DRY", partSupplier = "fixture", extraFields = Array.Empty<object>(), dryRun = true }, "/api/vehicle/supplyrecords?vehicleId=0")
            };
            using var verify = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:18080") };
            verify.DefaultRequestHeaders.Add("x-api-key", "workshop-test-only");
            foreach (var (name, arguments, path) in dryWrites)
            {
                var before = await verify.GetStringAsync(path);
                await Call(name, arguments);
                Assert(before == await verify.GetStringAsync(path), "Dry-run write changed records: " + name);
            }
            foreach (var type in new[] { "Service", "Repair", "Upgrade" })
            {
                var path = $"/api/vehicle/{type.ToLowerInvariant()}records?vehicleId={vehicle}";
                var before = await verify.GetStringAsync(path);
                await Call("Add" + type + "Record", new { vehicleId = vehicle, date = "2026-11-13", odometer = 123, description = "Dry workshop", cost = 350, extraFields = Array.Empty<object>(), requisitions = new[] { new { supplyRecordId = supply, quantity = .1m } }, dryRun = true });
                Assert(before == await verify.GetStringAsync(path), "Dry-run workshop changed records");
            }
            var fields = Array.Empty<object>();
            var part = await Call("AddSupplyRecord", new { vehicleId = vehicle, date = "2026-11-13", description = "MCP part", quantity = 2, cost = 100, partNumber = "MCP", partSupplier = "fixture", extraFields = fields, dryRun = false });
            var addedPart = JsonNode.Parse(part["content"]![0]!["text"]!.GetValue<string>())!;
            var partId = addedPart["additionalData"]!["recordId"]!.GetValue<int>();
            var pdf = Convert.ToBase64String("%PDF-1.7\n%%EOF"u8.ToArray());
            var result = await Call("AddServiceRecord", new { vehicleId = vehicle, date = "2026-11-13", odometer = 123, description = "MCP invoice", cost = 350, extraFields = fields, labourCost = 250, partsCost = 100, requisitions = new[] { new { supplyRecordId = partId, quantity = 1 } }, attachments = new[] { new { fileName = "invoice.pdf", contentType = "application/pdf", contentBase64 = pdf } } });
            var added = JsonNode.Parse(result["content"]![0]!["text"]!.GetValue<string>())!;
            Assert(added["success"]?.GetValue<bool>() == true, "MCP invoice add failed: " + added);
            var id = added["additionalData"]!["recordId"]!.GetValue<int>();
            var invoice = await Call("get_record_by_id", new { recordType = "service", recordId = id });
            Assert(invoice.ToJsonString().Contains("invoice.pdf"), "Invoice file not attached");
            Assert(invoice.ToJsonString().Contains("Labour Cost") && invoice.ToJsonString().Contains("250"), "Labour field missing");
            foreach (var type in new[] { "repair", "upgrade" })
            {
                var newJob = await Call("add_" + type + "_record", new { vehicleId = vehicle, date = "2026-11-13", odometer = 124, description = "MCP " + type, cost = 100, extraFields = fields });
                var jobId = JsonNode.Parse(newJob["content"]![0]!["text"]!.GetValue<string>())!["additionalData"]!["recordId"]!.GetValue<int>();
                await Call("update_" + type + "_record", new { recordId = jobId, cost = 110, dryRun = true });
                await Call("update_" + type + "_record", new { recordId = jobId, cost = 120 });
                await Call("get_" + type + "_records", new { vehicleId = vehicle });
            }
            await Call("get_service_records", new { vehicleId = vehicle });
            await Call("get_supply_records", new { vehicleId = vehicle });
            await Call("get_shop_supply_records", new { });
            var unchanged = invoice.ToJsonString();
            await Call("update_service_record", new { recordId = id, cost = 400, labourCost = 300, dryRun = true });
            Assert(unchanged == (await Call("get_record_by_id", new { recordType = "service", recordId = id })).ToJsonString(), "Edit dry run changed record");
            await Call("update_service_record", new { recordId = id, description = "Edited MCP invoice", cost = 400, labourCost = 300 });
            invoice = await Call("get_record_by_id", new { recordType = "service", recordId = id });
            Assert(invoice.ToJsonString().Contains("invoice.pdf") && invoice.ToJsonString().Contains("Edited MCP invoice") && invoice.ToJsonString().Contains("300"), "Edit lost files or labour");
            var stockBefore = (await Call("get_record_by_id", new { recordType = "supply", recordId = partId })).ToJsonString();
            await Call("update_supply_record", new { recordId = partId, description = "Edited part", dryRun = true });
            Assert(stockBefore == (await Call("get_record_by_id", new { recordType = "supply", recordId = partId })).ToJsonString(), "Supply edit dry run changed stock");
            await Call("update_supply_record", new { recordId = partId, description = "Edited part" });
            Assert((await Call("get_record_by_id", new { recordType = "supply", recordId = partId })).ToJsonString().Contains("Edited part"), "Supply edit failed");
            var bad = await Call("attach_document_to_record", new { recordType = "service", recordId = id, fileName = "bad.pdf", contentType = "application/pdf", contentBase64 = "not-base64" }, false);
            Assert(bad["isError"]?.GetValue<bool>() == true, "Invalid base64 was accepted");
            await Call("attach_document_to_record", new { recordType = "service", recordId = id, fileName = "dry.pdf", contentType = "application/pdf", contentBase64 = pdf, dryRun = true });
            invoice = await Call("get_record_by_id", new { recordType = "service", recordId = id });
            Assert(!invoice.ToJsonString().Contains("dry.pdf"), "Attachment dry run wrote a file");
        }
    }
}
