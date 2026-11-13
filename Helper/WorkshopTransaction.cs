using CarCareTracker.Models;
using LiteDB;
using Npgsql;
using System.Data;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace CarCareTracker.Helper;

// One transaction owns the record, stock and both requisition histories.
public sealed class WorkshopTransaction : IDisposable
{
    private readonly LiteDatabase? lite;
    private readonly NpgsqlConnection? pg;
    private readonly NpgsqlTransaction? transaction;
    private bool completed;
    private static readonly HashSet<string> Tables = ["servicerecords", "collisionrecords", "upgraderecords", "supplyrecords", "odometerrecords", "equipmentrecords"];

    public WorkshopTransaction(IConfiguration config, ILiteDBHelper helper)
    {
        var connection = config["POSTGRES_CONNECTION"];
        if (string.IsNullOrWhiteSpace(connection))
        {
            lite = helper.GetLiteDB();
            if (!lite.BeginTrans()) throw new InvalidOperationException("Could not start workshop transaction");
            try
            {
                // Acquire the stock collection's write lock before reading any balances.
                lite.GetCollection<SupplyRecord>("supplyrecords").Query().ForUpdate().Limit(1).ToList();
            }
            catch { lite.Rollback(); throw; }
        }
        else
        {
            pg = new NpgsqlConnection(connection);
            try { pg.Open(); transaction = pg.BeginTransaction(IsolationLevel.Serializable); }
            catch { pg.Dispose(); throw; }
        }
    }

    private static void CheckTable(string table)
    {
        if (!Tables.Contains(table)) throw new ArgumentException("Unsupported record type");
    }

    public T? Get<T>(string table, int id) where T : class
    {
        CheckTable(table);
        if (lite != null) return lite.GetCollection<T>(table).FindById(id);
        using var cmd = new NpgsqlCommand($"SELECT data FROM app.{table} WHERE id=@id FOR UPDATE", pg, transaction);
        cmd.Parameters.AddWithValue("id", id);
        var json = cmd.ExecuteScalar() as string;
        return json == null ? null : JsonSerializer.Deserialize<T>(json);
    }

    public int Save<T>(string table, T value, int id, int vehicleId) where T : class
    {
        CheckTable(table);
        if (lite != null)
        {
            var collection = lite.GetCollection<T>(table);
            if (id == 0) return collection.Insert(value).AsInt32;
            if (!collection.Update(id, value)) throw new InvalidOperationException("Record no longer exists");
            return id;
        }
        if (id == 0)
        {
            using var insert = new NpgsqlCommand($"INSERT INTO app.{table}(vehicleId,data) VALUES(@vehicleId,'{{}}') RETURNING id", pg, transaction);
            insert.Parameters.AddWithValue("vehicleId", vehicleId);
            id = Convert.ToInt32(insert.ExecuteScalar());
            typeof(T).GetProperty("Id")!.SetValue(value, id);
        }
        using var update = new NpgsqlCommand($"UPDATE app.{table} SET data=CAST(@data AS jsonb) WHERE id=@id", pg, transaction);
        update.Parameters.AddWithValue("id", id);
        update.Parameters.AddWithValue("data", JsonSerializer.Serialize(value));
        if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("Record no longer exists");
        return id;
    }

    public void Commit()
    {
        if (lite != null) lite.Commit(); else transaction!.Commit();
        completed = true;
    }

    public void Dispose()
    {
        try { if (!completed) { if (lite != null) lite.Rollback(); else transaction?.Rollback(); } }
        finally { transaction?.Dispose(); pg?.Dispose(); }
    }
}
